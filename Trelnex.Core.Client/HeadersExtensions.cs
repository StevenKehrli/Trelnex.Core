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
    /// Adds the Authorization Header from the <see cref="AccessToken"/>.
    /// </summary>
    /// <param name="headers">The specified <see cref="HttpRequestHeaders"/>.</param>
    /// <param name="getAuthorizationHeader">The function to get the authorization header value.</param>
    /// <returns>The <see cref="HttpRequestHeaders"/>.</returns>
    public static HttpRequestHeaders AddAuthorizationHeader(
        this HttpRequestHeaders headers,
        Func<string> getAuthorizationHeader)
    {
        headers.Add(
            name: HttpRequestHeader.Authorization.ToString(),
            value: getAuthorizationHeader());

        return headers;
    }
}
