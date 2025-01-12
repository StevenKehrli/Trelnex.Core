using Azure.Core;

namespace Trelnex.Core.Client.Identity;

public static class TokenCredentialExtensions
{
    public static string GetAuthorizationHeader(
        this TokenCredential tokenCredential,
        TokenRequestContext tokenRequestContext)
    {
        // get the access token
        var accessToken = tokenCredential.GetToken(tokenRequestContext, default);

        // format the authorization header
        return $"{accessToken.TokenType} {accessToken.Token}";
    }
}
