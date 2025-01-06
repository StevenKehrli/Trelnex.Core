namespace Trelnex.Core.Data;

public interface ICosmosCommandProviderStatus
{
    CosmosCommandProviderStatus GetStatus();
}

public record CosmosCommandProviderStatus(
    string AccountEndpoint,
    string DatabaseId,
    string[] ContainerIds,
    bool IsHealthy,
    string? Error);
