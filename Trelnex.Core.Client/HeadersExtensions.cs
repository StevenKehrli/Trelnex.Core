using System.Net;
using System.Net.Http.Headers;
using Azure.Core;

namespace Trelnex.Core.Client;

/// <summary>
/// Extension methods for <see cref="HttpRequestHeaders"/>.
/// </summary>
public static class HeadersExtensions
{
    /// <summary>
    /// Adds the Bearer Token Authorization Header from the <see cref="AccessToken"/>.
    /// </summary>
    /// <param name="headers">The specified <see cref="HttpRequestHeaders"/>.</param>
    /// <param name="tokenCredential">The specified <see cref="TokenCredential"/> to get the <see cref="AccessToken"/> for the specified set of scopes.</param>
    /// <param name="tokenRequestContext">The <see cref="TokenRequestContext"/> with authentication information.</param>
    /// <returns>The <see cref="HttpRequestHeaders"/>.</returns>
    public static HttpRequestHeaders AddBearerToken(
        this HttpRequestHeaders headers,
        TokenCredential tokenCredential,
        TokenRequestContext tokenRequestContext)
    {
        var accessToken = tokenCredential.GetToken(tokenRequestContext, default);

        headers.Add(HttpRequestHeader.Authorization.ToString(), $"Bearer {accessToken.Token}");

        return headers;
    }
}
