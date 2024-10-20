namespace Trelnex.Core.Api.Authentication;

/// <summary>
/// Defines the contract for a security requirement.
/// </summary>
/// <remark>
/// The security requirement is used by the Swagger <see cref="AuthorizeFilter"/>
/// to add the <see cref="OpenApiSecurityRequirement"/> to the operation (endpoint) documentation.
/// </remark>
public interface ISecurityRequirement
{
    /// <summary>
    /// The JWT bearer token scheme.
    /// </summary>
    public string JwtBearerScheme { get; }

    /// <summary>
    /// The URI to identify the required scope of the JWT bearer token.
    /// </summary>
    public string IdentifierURI { get; }

    /// <summary>
    /// The policy name for this requirement.
    /// </summary>
    public string Policy { get; }

    /// <summary>
    /// The array of roles required by this requirement.
    /// </summary>
    public string[] RequiredRoles { get; }
}

/// <summary>
/// Initializes a new instance of <see cref="SecurityRequirement"/>.
/// </summary>
/// <param name="jwtBearerScheme">The JWT bearer token scheme.</param>
/// <param name="identifierURI">The URI to identify the required scope of the JWT bearer token.</param>
/// <param name="policy">The policy name for this requirement.</param>
/// <param name="requiredRoles">The array of roles required by this requirement.</param>
internal class SecurityRequirement(
    string jwtBearerScheme,
    string identifierURI,
    string policy,
    string[] requiredRoles) : ISecurityRequirement
{
    /// <summary>
    /// The JWT bearer token scheme.
    /// </summary>
    public string JwtBearerScheme => jwtBearerScheme;

    /// <summary>
    /// The URI to identify the required scope of the JWT bearer token.
    /// </summary>
    public string IdentifierURI => identifierURI;

    /// <summary>
    /// The policy name for this requirement.
    /// </summary>
    public string Policy => policy;

    /// <summary>
    /// The array of roles required by this requirement.
    /// </summary>
    public string[] RequiredRoles => requiredRoles;
}
