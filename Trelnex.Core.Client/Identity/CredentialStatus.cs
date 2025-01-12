namespace Trelnex.Core.Client.Identity;

/// <summary>
/// Gets the status of a <see cref="TokenCredential"/>.
/// </summary>
/// <param name="CredentialName">Gets the name associated with the TokenCredential.</param>
/// <param name="getAccessTokenStatus">The function that retrieves the array of <see cref="AccessTokenStatus"/>.</param>
public class CredentialStatus(
    string credentialName,
    Func<AccessTokenStatus[]> getAccessTokenStatus)
{
    public string CredentialName => credentialName;

    public AccessTokenStatus[] Statuses => getAccessTokenStatus();
}
