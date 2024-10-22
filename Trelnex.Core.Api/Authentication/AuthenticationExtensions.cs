using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Web.TokenCacheProviders.InMemory;

namespace Trelnex.Core.Api.Authentication;

/// <summary>
/// Extension methods to add Authentication and Authorization to the <see cref="IServiceCollection"/>.
/// </summary>
public static class AuthenticationExtensions
{
    private static string? _method = null;

    public static bool IsReady => _method is not null;

    /// <summary>
    /// Add Authentication and Authorization to the <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <param name="configuration">Represents a set of key/value application configuration properties.</param>
    /// <returns>The <see cref="IServiceCollection"/>.</returns>
    public static IPermissionsBuilder AddAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        if (IsReady)
        {
            throw new InvalidOperationException($"{_method} has already been called.");
        }

        _method = nameof(AddAuthentication);

        services.AddInMemoryTokenCaches();

        // inject our security provider
        var securityProvider = new SecurityProvider();
        services.AddSingleton<ISecurityProvider>(securityProvider);

        // add the permissions to the security provider
        return new PermissionsBuilder(services, configuration, securityProvider);
    }

    /// <summary>
    /// Add Authentication and Authorization to the <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <returns>The <see cref="IServiceCollection"/>.</returns>
    public static void NoAuthentication(
        this IServiceCollection services)
    {
        if (IsReady)
        {
            throw new InvalidOperationException($"{_method} has already been called.");
        }

        _method = nameof(NoAuthentication);

        services.AddHttpContextAccessor();

        services.AddAuthentication();
        services.AddAuthorization();

        // inject an empty security provider
        var securityProvider = new SecurityProvider();
        services.AddSingleton<ISecurityProvider>(securityProvider);
    }
}
