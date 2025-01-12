namespace Trelnex.Core.Client.Identity;

public record CredentialFactoryOptions
{
    public CredentialSource[] Sources { get; init; } = null!;
}
