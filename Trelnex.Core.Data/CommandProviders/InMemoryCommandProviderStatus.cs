namespace Trelnex.Core.Data;

public interface IInMemoryCommandProviderStatus
{
    InMemoryCommandProviderStatus GetStatus();
}

public record InMemoryCommandProviderStatus(
    bool IsHealthy,
    string? Error);
