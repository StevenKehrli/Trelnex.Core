using Azure.Core;

namespace Trelnex.Core.Client.Identity;

/// <summary>
/// Represents the status of an access token within a TokenCredential.
/// </summary>
/// <param name="Health">A value describing the health of the access token. See <see cref="AccessTokenHealth"/>.</param>
/// <param name="TokenRequestContext">The details of the access token request that created the access token. See <see cref="TokenRequestContext"/>.</param>
/// <param name="ExpiresOn">The time when the access token expires. See <see cref="AccessToken.ExpiresOn"/>.</param>
public record AccessTokenStatus(
    AccessTokenHealth Health,
    TokenRequestContext TokenRequestContext,
    DateTimeOffset? ExpiresOn);
