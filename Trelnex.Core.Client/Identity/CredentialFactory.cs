using System.Collections.Concurrent;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Logging;

namespace Trelnex.Core.Client.Identity;

/// <summary>
/// A Factory pattern to create a <see cref="TokenCredential"/>.
/// </summary>
public class CredentialFactory
{
    /// <summary>
    /// The singleton instance of the <see cref="CredentialFactory"/>.
    /// </summary>
    private static CredentialFactory _instance = null!;

    /// <summary>
    /// The <see cref="ILogger"/>.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// The underlying <see cref="TokenCredential"/>.
    /// </summary>
    private readonly TokenCredential _credential;

    /// <summary>
    /// A thread-safe collection of <see cref="string"/> to <see cref="NamedCredential"/>.
    /// </summary>
    private readonly ConcurrentDictionary<string, Lazy<NamedCredential>> _namedCredentialsByName = new();

    private CredentialFactory(
        ILogger logger,
        TokenCredential credential)
    {
        _logger = logger;
        _credential = credential;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CredentialFactory"/> class.
    /// </summary>
    /// <param name="logger">The <see cref="ILogger"/>.</param>
    /// <param name="options">The <see cref="CredentialFactoryOptions"/>.</param>
    /// <returns>The <see cref="CredentialFactory"/>.</returns>
    public static void Initialize(
        ILogger logger,
        CredentialFactoryOptions options)
    {
        // create the credential
        if (options?.Sources == null || options.Sources.Length == 0)
        {
            throw new ArgumentNullException(nameof(options.Sources));
        }

        var sources = options.Sources
            .Select(source => source switch
            {
                CredentialSource.WorkloadIdentity => new WorkloadIdentityCredential() as TokenCredential,
                CredentialSource.AzureCli => new AzureCliCredential() as TokenCredential,
                _ => throw new ArgumentOutOfRangeException()
            })
            .ToArray();

        var credential = new ChainedTokenCredential(sources);

        _instance = new CredentialFactory(logger, credential);
    }

    /// <summary>
    /// Gets the singleton instance of the <see cref="CredentialFactory"/>.
    /// </summary>
    public static CredentialFactory Instance => _instance ?? throw new InvalidOperationException("CredentialFactory has not been initialized.");

    /// <summary>
    /// Gets a value indicating whether the <see cref="CredentialFactory"/> has been initialized.
    /// </summary>
    public static bool IsInitialized => _instance != null;

    /// <summary>
    /// Gets the <see cref="TokenCredential"/> for the specified credential name.
    /// </summary>
    /// <param name="credentialName">The name of the specified credential.</param>
    /// <returns>The <see cref="TokenCredential"/>.</returns>
    public TokenCredential Get(
        string credentialName)
    {
        // https://andrewlock.net/making-getoradd-on-concurrentdictionary-thread-safe-using-lazy/
        var lazyNamedCredential =
            _namedCredentialsByName.GetOrAdd(
                key: credentialName,
                value: new Lazy<NamedCredential>(
                    () => new NamedCredential(_logger, credentialName, _credential)));

        return lazyNamedCredential.Value;
    }

    /// <summary>
    /// Gets the array of <see cref="CredentialStatus"/> for all credentials.
    /// </summary>
    /// <returns>The array of <see cref="CredentialStatus"/>.</returns>
    public CredentialStatus[] GetStatus()
    {
        return _namedCredentialsByName
            .Select(kvp =>
            {
                var credentialName = kvp.Key;

                var lazyNamedCredential = kvp.Value;
                var namedCredential = lazyNamedCredential.Value;

                return new CredentialStatus(
                    credentialName: credentialName,
                    getAccessTokenStatus: namedCredential.GetStatus);
            })
            .OrderBy(status => status.CredentialName)
            .ToArray();
    }
}
