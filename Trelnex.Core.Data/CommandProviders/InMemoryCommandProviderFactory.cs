using FluentValidation;

namespace Trelnex.Core.Data;

/// <summary>
/// A builder for creating an instance of the <see cref="CosmosCommandProvider"/>.
/// </summary>
public class InMemoryCommandProviderFactory : IInMemoryCommandProviderStatus
{
    private readonly Func<InMemoryCommandProviderStatus> _getStatus;

    private InMemoryCommandProviderFactory(
        Func<InMemoryCommandProviderStatus> getStatus)
    {
        _getStatus = getStatus;
    }

    /// <summary>
    /// Create an instance of the <see cref="InMemoryCommandProviderFactory"/>.
    /// </summary>
    /// <returns>The <see cref="InMemoryCommandProviderFactory"/>.</returns>
    public static async Task<InMemoryCommandProviderFactory> Create()
    {
        InMemoryCommandProviderStatus getStatus()
        {
            return new InMemoryCommandProviderStatus(
                IsHealthy: true,
                Error: null);
        }

        var factory = new InMemoryCommandProviderFactory(
            getStatus);

        return await Task.FromResult(factory);
    }


    /// <summary>
    /// Create an instance of the <see cref="InMemoryCommandProvider"/>.
    /// </summary>
    /// <param name="typeName">The type name of the item - used for <see cref="BaseItem.TypeName"/>.</param>
    /// <param name="validator">The fluent validator for the item.</param>
    /// <param name="commandOperations">The value indicating if update and delete commands are allowed. By default, update is allowed; delete is not allowed.</param>
    /// <typeparam name="TInterface">The specified interface type.</typeparam>
    /// <typeparam name="TItem">The specified item type that implements the specified interface type.</typeparam>
    /// <returns>The <see cref="InMemoryCommandProvider"/>.</returns>
    public ICommandProvider<TInterface> Create<TInterface, TItem>(
        string typeName,
        AbstractValidator<TItem>? validator = null,
        CommandOperations? commandOperations = null)
        where TInterface : class, IBaseItem
        where TItem : BaseItem, TInterface, new()
    {
        return new InMemoryCommandProvider<TInterface, TItem>(
            typeName,
            validator,
            commandOperations);
    }

    public InMemoryCommandProviderStatus GetStatus() => _getStatus();
}
