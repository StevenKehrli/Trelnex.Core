using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Trelnex.Core.Client.Identity;

namespace Trelnex.Core.Api.HealthChecks;

/// <summary>
/// Extension methods to add the access token health checks to the <see cref="IHealthChecksBuilder"/>.
/// </summary>
public static class AccessTokenHealthCheckExtensions
{
    /// <summary>
    /// Add the access token health checks to the <see cref="IHealthChecksBuilder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IHealthChecksBuilder"/> to add the additional health checks to.</param>
    /// <returns>The <see cref="IHealthChecksBuilder"/>.</returns>
    public static IHealthChecksBuilder AddAccessTokenHealthChecks(
        this IHealthChecksBuilder builder)
    {
        if (CredentialFactory.IsInitialized is false) return builder;

        // add the credential health checks
        Array.ForEach(CredentialFactory.Instance.CredentialNames, credentialName =>
        {
            var healthCheckName = $"AccessTokenHealthCheck: {credentialName}";

            builder.Add(
                new HealthCheckRegistration(
                    name: healthCheckName,
                    factory: _ => new AccessTokenHealthCheck(credentialName),
                    failureStatus: null,
                    tags: null));
        });

        return builder;
    }
}
