using System.Text.Json.Serialization;

namespace Trelnex.Core.Client.Identity;

/// <summary>
/// Represents the reported status of an access token.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AccessTokenHealth
{
    /// <summary>
    /// Indicates that the access token is expired.
    /// </summary>
    Expired = 0,

    /// <summary>
    /// Indicates that the access token is valid.
    /// </summary>
    Valid = 1,
}
