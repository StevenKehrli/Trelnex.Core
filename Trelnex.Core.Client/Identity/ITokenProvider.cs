using Azure.Core;

namespace Trelnex.Core.Client.Identity;

public interface ITokenProvider
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
}
