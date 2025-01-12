using Azure.Core;

namespace Trelnex.Core.Client.Identity;

public interface IAccessTokenProvider
{
    /// <summary>
    /// Gets the status of this token provider.
    /// </summary>
    /// <returns>The <see cref="TokenProviderStatus"/> describing the status of this token provider.</returns>
    AccessTokenStatus GetStatus();

    /// <summary>
    /// Gets the <see cref="AccessToken"/>.
    /// </summary>
    /// <returns>The <see cref="AccessToken"/>.</returns>
    AccessToken GetToken();

    /// <summary>
    /// Gets the <see cref="AccessToken"/>.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to use.</param>
    /// <returns>The <see cref="AccessToken"/>.</returns>
    ValueTask<AccessToken> GetTokenAsync(
        CancellationToken cancellationToken);
}
