using Azure.Core;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Trelnex.Core.Client.Identity;

namespace Trelnex.Core.Api.HealthChecks;

/// <summary>
/// Initializes a new instance of the <see cref="AccessTokenHealthCheck"/>.
/// </summary>
/// <param name="credentialName">The name of the <see cref="TokenCredential"/> for this health check.</param>
internal class AccessTokenHealthCheck(
    string credentialName) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = new CancellationToken())
    {
        var credentialStatus = CredentialFactory.Instance.GetStatus(credentialName);

        var data = new Dictionary<string, object>()
        {
            ["statuses"] = credentialStatus.Statuses
        };

        var healthCheckResult = new HealthCheckResult(
            status: GetHealthStatus(credentialStatus.Statuses),
            description: credentialName,
            data: data);

        return Task.FromResult(healthCheckResult);
    }

    /// <summary>
    /// Gets the <see cref="HealthStatus"/> from the array of <see cref="AccessTokenStatus"/>.
    /// </summary>
    /// <param name="statuses">The array of <see cref="AccessTokenStatus"/>.</param>
    /// <returns>A <see cref="HealthStatus"/> that represents the reported status of the health check result.</returns>
    private static HealthStatus GetHealthStatus(
        AccessTokenStatus[] statuses)
    {
        if (statuses.Length <= 0) return HealthStatus.Unhealthy;

        return (statuses.Any(ats => ats.Health == AccessTokenHealth.Expired))
            ? HealthStatus.Unhealthy
            : HealthStatus.Healthy;
    }
}
