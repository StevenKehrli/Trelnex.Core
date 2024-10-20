namespace Trelnex.Core.Api.Authentication;

/// <summary>
/// Defines the contract for a security definition.
/// </summary>
/// <remark>
/// The security definition is used by the Swagger <see cref="SecurityFilter"/>
/// to add the <see cref="OpenApiSecurityScheme"/> to the Swagger documentation.
/// </remark>
public interface ISecurityDefinition
{
    /// <summary>
    /// The JWT bearer token scheme.
    /// </summary>
    public string JwtBearerScheme { get; }

    /// <summary>
    /// The URI to identify the required scope of the JWT bearer token.
    /// </summary>
    public string IdentifierURI { get; }
}

/// <summary>
/// Initializes a new instance of the <see cref="SecurityDefinition"/>.
/// </summary>
/// <param name="jwtBearerScheme">Specifies the JWT bearer token scheme.</param>
/// <param name="identifierURI">Specifies the URI to identify the required scope of the JWT bearer token.</param>
internal class SecurityDefinition(
    string jwtBearerScheme,
    string identifierURI) : ISecurityDefinition
{
    public string JwtBearerScheme => jwtBearerScheme;
    public string IdentifierURI => identifierURI;
}
