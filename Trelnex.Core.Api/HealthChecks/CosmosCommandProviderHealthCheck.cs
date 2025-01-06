using Microsoft.Extensions.Diagnostics.HealthChecks;
using Trelnex.Core.Data;

namespace Trelnex.Core.Api.HealthChecks;

/// <summary>
/// Initializes a new instance of the <see cref="CosmosCommandProviderHealthCheck"/>.
/// </summary>
/// <param name="providerStatus">The <see cref="ICosmosCommandProviderStatus"/> to get the status.</param>
internal class CosmosCommandProviderHealthCheck(
    ICosmosCommandProviderStatus providerStatus) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = new CancellationToken())
    {
        var status = providerStatus.GetStatus();

        var data = new Dictionary<string, object>()
        {
            ["accountEndpoint"] = status.AccountEndpoint,
            ["databaseId"] = status.DatabaseId,
            ["containerIds"] = status.ContainerIds,
        };

        if (status.IsHealthy is false && status.Error is not null)
        {
            data["error"] = status.Error;
        }

        var healthCheckResult = new HealthCheckResult(
            status: status.IsHealthy ? HealthStatus.Healthy : HealthStatus.Unhealthy,
            data: data);

        return Task.FromResult(healthCheckResult);
    }
}
