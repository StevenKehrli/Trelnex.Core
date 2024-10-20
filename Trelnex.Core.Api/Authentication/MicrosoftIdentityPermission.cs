using System.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Web;

namespace Trelnex.Core.Api.Authentication;

/// <summary>
/// An implementation of <see cref="IPermission"/> using Microsoft Identity.
/// </summary>
public abstract class MicrosoftIdentityPermission : IPermission
{
    /// <summary>
    /// Gets the configuration section name to configure this <see cref="IPermission"/>.
    /// </summary>
    protected abstract string ConfigSectionName { get; }

    /// <summary>
    /// Gets the JWT bearer token scheme.
    /// </summary>
    public abstract string JwtBearerScheme { get; }

    /// <summary>
    /// Add Authentication to the <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <param name="configuration">Represents a set of key/value application configuration properties.</param>
    public void AddAuthentication(
        IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMicrosoftIdentityWebApiAuthentication(
            configuration,
            ConfigSectionName,
            JwtBearerScheme);
    }

    /// <summary>
    /// Add <see cref="IPermissionPolicy"/> to the <see cref="IPoliciesBuilder"/>.
    /// </summary>
    /// <param name="policiesBuilder">The <see cref="IPoliciesBuilder"/> to add the policies to the permission.</param>
    public abstract void AddAuthorization(
        IPoliciesBuilder policiesBuilder);

    /// <summary>
    /// Gets the URI to identify the required scope of the JWT bearer token.
    /// </summary>
    public string GetIdentifierURI(
        IConfiguration configuration)
    {
        var identifierURI = configuration.GetSection(ConfigSectionName).GetValue<string>("IdentifierURI");
        if (string.IsNullOrWhiteSpace(identifierURI))
        {
            throw new ConfigurationErrorsException($"{ConfigSectionName}:IdentifierURI");
        }

        return identifierURI;
    }
}
