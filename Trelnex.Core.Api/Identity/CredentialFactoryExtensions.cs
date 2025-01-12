using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Trelnex.Core.Client.Identity;

namespace Trelnex.Core.Api.Identity;

public static class CredentialFactoryExtensions
{
    /// <summary>
    /// Add the <see cref="CredentialFactory"/> to the <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <param name="configuration">Represents a set of key/value application configuration properties.</param>
    /// <param name="bootstrapLogger">The <see cref="ILogger"/> to write the CommandProvider bootstrap logs.</param>
    /// <returns>The <see cref="IServiceCollection"/>.</returns>
    public static IServiceCollection AddCredentialFactory(
        this IServiceCollection services,
        IConfiguration configuration,
        ILogger bootstrapLogger)
    {
        var options = configuration.GetSection("CredentialFactory").Get<CredentialFactoryOptions>();
        if (options is null) return services;

        // create the credential factory
        CredentialFactory.Initialize(
            bootstrapLogger,
            options);

        return services;
    }
}
