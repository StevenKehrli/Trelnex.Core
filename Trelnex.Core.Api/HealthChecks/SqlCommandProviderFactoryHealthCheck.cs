using Microsoft.Extensions.Diagnostics.HealthChecks;
using Trelnex.Core.Data;

namespace Trelnex.Core.Api.HealthChecks;

/// <summary>
/// Initializes a new instance of the <see cref="SqlCommandProviderHealthCheck"/>.
/// </summary>
/// <param name="providerStatus">The <see cref="ISqlCommandProviderStatus"/> to get the status.</param>
internal class SqlCommandProviderHealthCheck(
    ISqlCommandProviderStatus providerStatus) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = new CancellationToken())
    {
        var status = providerStatus.GetStatus();

        var data = new Dictionary<string, object>()
        {
            ["dataSource"] = status.DataSource,
            ["initialCatalog"] = status.InitialCatalog,
            ["tableNames"] = status.TableNames,
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
