namespace Trelnex.Core.Data;

public interface ISqlCommandProviderStatus
{
    SqlCommandProviderStatus GetStatus();
}

public record SqlCommandProviderStatus(
    string DataSource,
    string InitialCatalog,
    string[] TableNames,
    bool IsHealthy,
    string[]? Version,
    string? Error);
