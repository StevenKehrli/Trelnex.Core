using System.Collections;
using System.Collections.Concurrent;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Logging;

namespace Trelnex.Core.Client.Identity;

/// <summary>
/// Enables authentication to Microsoft Entra ID to obtain an access token.
/// </summary>
/// <remarks>
/// <para>
/// NamedCredential is an internal class. Users will get an instance of the <see cref="TokenCredential"/> abstract class.
/// </para>
/// <para>
/// A NamedCredential is addressable by the credential name within <see cref="CredentialFactory"/>.
/// </para>
/// </remarks>
/// <param name="logger">The <see cref="ILogger"> used to perform logging.</param>
/// <param name="credentialName">The name of this <see cref="TokenCredential"/>.</param>
/// <param name="tokenCredential">The underlying <see cref="ChainedTokenCredential"/> (<see cref="WorkloadIdentityCredential"/> and <see cref="AzureCliCredential"/>).</param>
internal class NamedCredential(
    ILogger logger,
    string credentialName,
    TokenCredential tokenCredential) : TokenCredential
{
    /// <summary>
    /// A thread-safe collection of <see cref="TokenRequestContext"/> to <see cref="AccessTokenProvider"/>.
    /// </summary>
    private readonly ConcurrentDictionary<TokenRequestContextKey, Lazy<AccessTokenProvider>> _accessTokenProvidersByTokenRequestContextKey = new();

    public override AccessToken GetToken(
        TokenRequestContext tokenRequestContext,
        CancellationToken cancellationToken)
    {
        // create a TokenRequestContextKey - we do not care about ParentRequestId
        var key = TokenRequestContextKey.FromTokenRequestContext(tokenRequestContext);

        // https://andrewlock.net/making-getoradd-on-concurrentdictionary-thread-safe-using-lazy/
        var lazyAccessTokenProvider =
            _accessTokenProvidersByTokenRequestContextKey.GetOrAdd(
                key: key,
                value: new Lazy<AccessTokenProvider>(
                    AccessTokenProvider.Create(
                        logger,
                        credentialName,
                        tokenCredential,
                        key)));

        return lazyAccessTokenProvider.Value.GetToken();
    }

    public override ValueTask<AccessToken> GetTokenAsync(
        TokenRequestContext tokenRequestContext,
        CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(
            GetToken(tokenRequestContext, cancellationToken));
    }

    /// <summary>
    /// Gets the <see cref="CredentialStatus"/> for this credential.
    /// </summary>
    /// <returns>A <see cref="CredentialStatus"/> describing the status of this credential.</returns>
    public CredentialStatus GetStatus()
    {
        var statuses = _accessTokenProvidersByTokenRequestContextKey.Select(kvp =>
        {
            var lazy = kvp.Value;
            var accessTokenProvider = lazy.Value;

            return accessTokenProvider.GetStatus();
        }).ToArray();

        return new CredentialStatus(credentialName, statuses);
    }

    /// <summary>
    /// Contains the details of an authentication token request.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is used as the key to <see cref="_accessTokenProvidersByTokenRequestContextKey"/>.
    /// </para>
    /// <para>
    /// This is a class (reference type) alternative to the struct (value type) of <see cref="TokenRequestContext"/>.
    /// </para>
    /// <para>
    /// This ignores the <see cref="TokenRequestContext.ParentRequestId"/> property.
    /// It is not used by <see cref="GetToken"/> and <see cref="GetTokenAsync"/>.
    /// </para>
    /// <para>
    /// This implements the <see cref="Equals"/> and <see cref="GetHashCode"/> necessary for the <see cref="_accessTokenProvidersByTokenRequestContextKey"/>.
    /// </para>
    /// </remarks>
    /// <param name="claims">Additional claims to be included in the token.</param>
    /// <param name="isCaeEnabled">Indicates whether to enable Continuous Access Evaluation (CAE) for the requested token.</param>
    /// <param name="scopes">The scopes required for the token.</param>
    /// <param name="tenantId">The tenantId to be included in the token request.</param>
    private class TokenRequestContextKey(
        string? claims,
        bool isCaeEnabled,
        string[] scopes,
        string? tenantId)
    {
        /// <summary>
        /// Additional claims to be included in the token. See <see href="https://openid.net/specs/openid-connect-core-1_0-final.html#ClaimsParameter">https://openid.net/specs/openid-connect-core-1_0-final.html#ClaimsParameter</see> for more information on format and content.
        /// </summary>
        public string? Claims => claims;

        /// <summary>
        /// Indicates whether to enable Continuous Access Evaluation (CAE) for the requested token.
        /// </summary>
        public bool IsCaeEnabled => isCaeEnabled;

        /// <summary>
        /// The scopes required for the token.
        /// </summary>
        public string[] Scopes => scopes;

        /// <summary>
        /// The tenantId to be included in the token request.
        /// </summary>
        public string? TenantId => tenantId;

        /// <summary>
        /// Converts a <see cref="TokenRequestContext"/> to a <see cref="TokenRequestContextKey"/>.
        /// </summary>
        /// <param name="tokenRequestContext">The <see cref="TokenRequestContext"/>.</param>
        /// <returns>A <see cref="TokenRequestContextKey"/>.</returns>
        public static TokenRequestContextKey FromTokenRequestContext(
            TokenRequestContext tokenRequestContext)
        {
            return new TokenRequestContextKey(
                claims: tokenRequestContext.Claims,
                isCaeEnabled: tokenRequestContext.IsCaeEnabled,
                scopes: tokenRequestContext.Scopes,
                tenantId: tokenRequestContext.TenantId);
        }

        /// <summary>
        /// Converts this <see cref="TokenRequestContextKey"/> to a <see cref="TokenRequestContext"/>.
        /// </summary>
        /// <returns>A <see cref="TokenRequestContext"/>.</returns>
        public TokenRequestContext ToTokenRequestContext()
        {
            return new TokenRequestContext(
                claims: Claims,
                isCaeEnabled: IsCaeEnabled,
                scopes: Scopes,
                tenantId: TenantId);
        }

        public override bool Equals(object? obj)
        {
            return (obj is TokenRequestContextKey other) && Equals(other);
        }

        /// <summary>
        /// Determines whether the specified <see cref="TokenRequestContextKey"/> is equal to the current object.
        /// </summary>
        /// <param name="other">The <see cref="TokenRequestContextKey"/> to compare with the current object.</param>
        /// <returns>true if the specified <see cref="TokenRequestContextKey"/> is equal to the current object; otherwise, false.</returns>
        private bool Equals(
            TokenRequestContextKey other)
        {
            if (string.Equals(Claims, other.Claims) is false) return false;

            if (IsCaeEnabled != other.IsCaeEnabled) return false;

            if (StructuralComparisons.StructuralEqualityComparer.Equals(Scopes, other.Scopes) is false) return false;

            if (string.Equals(TenantId, other.TenantId) is false) return false;

            return true;
        }

        public override int GetHashCode()
        {
            var hashCode = HashCode.Combine(
                claims,
                isCaeEnabled,
                StructuralComparisons.StructuralEqualityComparer.GetHashCode(scopes),
                tenantId);

            return hashCode;
        }
    }

    /// <summary>
    /// Combines an <see cref="AccessToken"/> with a <see cref="System.Threading.Timer"/> to refresh the <see cref="AccessToken"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An AccessTokenProvider will refresh its access token in accordance with <see cref="AccessToken.RefreshOn"/>.
    /// This will maintain a valid access token and enable <see cref="GetToken"/> or <see cref="GetTokenAsync"/> to return immediately.
    /// </para>
    /// </remarks>
    private class AccessTokenProvider : IAccessTokenProvider
    {
        /// <summary>
        /// The <see cref="ILogger"> used to perform logging.
        /// </summary>
        private readonly ILogger _logger;

        /// <summary>
        /// The name of this <see cref="TokenCredential"/>.
        /// </summary>
        private readonly string _credentialName;

        /// <summary>
        /// The underlying <see cref="TokenCredential"/>.
        /// </summary>
        private readonly TokenCredential _tokenCredential;

        /// <summary>
        /// The underlying <see cref="TokenRequestContextKey"/>.
        /// </summary>
        private readonly TokenRequestContextKey _tokenRequestContextKey;

        /// <summary>
        /// The <see cref="Timer"/> to refresh the <see cref="AccessToken"/>.
        /// </summary>
        private readonly Timer _timer;

        /// <summary>
        /// The underlying <see cref="AccessToken"/>.
        /// </summary>
        private AccessToken? _accessToken;

        /// <summary>
        /// The message to throw when the access token is unavailable.
        /// </summary>
        private string? _unavailableMessage;

        /// <summary>
        /// The inner exception to include when the access token is unavailable.
        /// </summary>
        private Exception? _unavailableInnerException;

        /// <summary>
        /// Initializes a new instance of the <see cref="AccessTokenProvider"/>.
        /// </summary>
        /// <param name="logger">The <see cref="ILogger"> used to perform logging.</param>
        /// <param name="credentialName">The name of this <see cref="TokenCredential"/>.</param>
        /// <param name="tokenCredential">The <see cref="TokenCredential"/> capable of providing a <see cref="AccessToken"/>.</param>
        /// <param name="tokenRequestContextKey">The <see cref="TokenRequestContextKey"/> containing the details of an authentication token request.</param>
        private AccessTokenProvider(
            ILogger logger,
            string credentialName,
            TokenCredential tokenCredential,
            TokenRequestContextKey tokenRequestContextKey)
        {
            _logger = logger;
            _credentialName = credentialName;
            _tokenCredential = tokenCredential;
            _tokenRequestContextKey = tokenRequestContextKey;

            _timer = new Timer(Refresh, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AccessTokenProvider"/>.
        /// </summary>
        /// <param name="logger">The <see cref="ILogger"> used to perform logging.</param>
        /// <param name="credentialName">The name of this <see cref="TokenCredential"/>.</param>
        /// <param name="tokenCredential">The <see cref="TokenCredential"/> capable of providing a <see cref="AccessToken"/>.</param>
        /// <param name="tokenRequestContextKey">The <see cref="TokenRequestContextKey"/> containing the details of an authentication token request.</param>
        public static AccessTokenProvider Create(
            ILogger logger,
            string credentialName,
            TokenCredential tokenCredential,
            TokenRequestContextKey tokenRequestContextKey)
        {
            // create the accessTokenProvider and schedule the refresh (to get its access token)
            // this will set _accessToken
            var accessTokenProvider = new AccessTokenProvider(
                logger,
                credentialName,
                tokenCredential,
                tokenRequestContextKey);

            accessTokenProvider.Refresh(null);

            return accessTokenProvider;
        }

        /// <summary>
        /// Gets the <see cref="AccessToken"/>.
        /// </summary>
        public AccessToken GetToken()
        {
            lock (this)
            {
                return _accessToken ?? throw new CredentialUnavailableException(_unavailableMessage, _unavailableInnerException);
            }
        }

        /// <summary>
        /// Gets the <see cref="AccessToken"/>.
        /// </summary>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> to use.</param>
        /// <returns>The <see cref="AccessToken"/>.</returns>
        public async ValueTask<AccessToken> GetTokenAsync(
            CancellationToken cancellationToken = default)
        {
            var accessToken = GetToken();

            return await ValueTask.FromResult(accessToken);
        }

        /// <summary>
        /// Gets the <see cref="AccessTokenStatus"/> for this access token.
        /// </summary>
        /// <returns>A <see cref="AccessTokenStatus"/> describing the status of this access token.</returns>
        public AccessTokenStatus GetStatus()
        {
            lock (this)
            {
                var health = ((_accessToken?.ExpiresOn ?? DateTimeOffset.MinValue) < DateTimeOffset.UtcNow)
                    ? AccessTokenHealth.Expired
                    : AccessTokenHealth.Valid;

                return new AccessTokenStatus(
                    Health: health,
                    TokenRequestContext: _tokenRequestContextKey.ToTokenRequestContext(),
                    ExpiresOn: _accessToken?.ExpiresOn);
            }
        }

        /// <summary>
        /// The <see cref="TimerCallback"/> delegate of the <see cref="_timer"/> <see cref="Timer"/>.
        /// </summary>
        /// <param name="state">An object containing application-specific information relevant to the method invoked by this delegate, or null.</param>
        private void Refresh(object? state)
        {
            _logger.LogInformation(
                "AccessTokenProvider.Refresh: '{credentialName:l}', claims: '{claims:l}', isCaeEnabled: '{isCaeEnabled}', scopes: '{scopes:l}', tenantId: '{tenantId:l}'...",
                _credentialName,
                _tokenRequestContextKey.Claims,
                _tokenRequestContextKey.IsCaeEnabled,
                string.Join(", ", _tokenRequestContextKey.Scopes),
                _tokenRequestContextKey.TenantId);

            // assume we need to refresh in 5 seconds
            var dueTime = TimeSpan.FromSeconds(5);

            try
            {
                // get a new access token and set
                var accessToken = _tokenCredential.GetToken(
                    _tokenRequestContextKey.ToTokenRequestContext(),
                    default);

                SetAccessToken(accessToken);

                // we got a token - schedule refresh
                // workloadIdentityCredential will have RefreshOn set - use RefreshOn
                // azureCliCredential will not - use ExpiresOn
                var refreshOn = accessToken.RefreshOn ?? accessToken.ExpiresOn;

                _logger.LogInformation(
                    "AccessTokenProvider.RefreshOn: '{credentialName:l}', claims: '{claims:l}', isCaeEnabled: '{isCaeEnabled}', scopes: '{scopes:l}', tenantId: '{tenantId:l}', refreshOn: '{refreshOn:o}'.",
                    _credentialName,
                    _tokenRequestContextKey.Claims,
                    _tokenRequestContextKey.IsCaeEnabled,
                    string.Join(", ", _tokenRequestContextKey.Scopes),
                    _tokenRequestContextKey.TenantId,
                    refreshOn);

                dueTime = refreshOn - DateTimeOffset.UtcNow;
            }
            catch (CredentialUnavailableException ex)
            {
                var errors = ParseUnavailable(ex);

                SetUnavailable(ex.Message, ex.InnerException);

                _logger.LogError(
                    "AccessTokenProvider.CredentialUnavailableException: '{credentialName:l}', claims: '{claims:l}', isCaeEnabled: '{isCaeEnabled}', scopes: '{scopes:l}', tenantId: '{tenantId:l}', message: '{message:}', errors: {errors}.",
                    _credentialName,
                    _tokenRequestContextKey.Claims,
                    _tokenRequestContextKey.IsCaeEnabled,
                    string.Join(", ", _tokenRequestContextKey.Scopes),
                    _tokenRequestContextKey.TenantId,
                    ex.Message,
                    errors);
            }
            catch
            {
            }

            _timer.Change(dueTime, Timeout.InfiniteTimeSpan);
        }

        /// <summary>
        /// Sets the <see cref="AccessToken"/> for this object.
        /// </summary>
        private void SetAccessToken(
            AccessToken accessToken)
        {
            lock (this)
            {
                _accessToken = accessToken;

                _unavailableMessage = null;
                _unavailableInnerException = null;
            }
        }

        /// <summary>
        /// Sets the message to throw when the access token is unavailable.
        /// </summary>
        /// <param name="message">The message to throw when the access token is unavailable.</param>
        /// <param name="innerException">The inner exception to include when the access token is unavailable.</param>
        private void SetUnavailable(
            string message,
            Exception? innerException)
        {
            lock (this)
            {
                _unavailableMessage = message;
                _unavailableInnerException = innerException;
            }
        }

        /// <summary>
        /// Parses the exceptions from the <see cref="CredentialUnavailableException"/> into an array of errors.
        /// </summary>
        /// <param name="ex">The <see cref="CredentialUnavailableException"/> to parse.</param>
        /// <returns>An array of errors containing the messages of the exceptions.</returns>
        private static string[]? ParseUnavailable(
            CredentialUnavailableException ex)
        {
            // get the inner exception as an aggregate exception
            var aggregateException = ex.InnerException as AggregateException;

            // return its inner exception messages as the array of errors
            return aggregateException?.InnerExceptions
                .Select(ie => ie.Message)
                .ToArray();
        }
    }
}
