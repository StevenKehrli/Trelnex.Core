using FluentValidation.Results;

namespace Trelnex.Core.Data;

/// <summary>
/// The class to expose and validate a item read from the backing data store.
/// </summary>
/// <typeparam name="TInterface">The interface type of the items in the backing data store.</typeparam>
public interface IQueryResult<TInterface>
    where TInterface : class, IBaseItem
{
    /// <summary>
    /// The item.
    /// </summary>
    TInterface Item { get; }

    /// <summary>
    /// Returns an <see cref="ISaveCommand{TInterface}"/> to delete the item.
    /// </summary>
    /// <returns>An <see cref="ISaveCommand{TInterface}"/> to delete the item.</returns>
    ISaveCommand<TInterface> Delete();

    /// <summary>
    /// Returns an <see cref="ISaveCommand{TInterface}"/> to update the item.
    /// </summary>
    /// <returns>An <see cref="ISaveCommand{TInterface}"/> to update the item.</returns>
    ISaveCommand<TInterface> Update();

    /// <summary>
    /// The action to validate the item.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>The fluent <see cref="ValidationResult"/>item that was saved.</returns>
    Task<ValidationResult> ValidateAsync(
        CancellationToken cancellationToken);
}

/// <summary>
/// The class to read the item in the backing data store.
/// </summary>
/// <typeparam name="TInterface">The interface type of the items in the backing data store.</typeparam>
internal class QueryResult<TInterface, TItem>
    : ProxyManager<TInterface, TItem>, IQueryResult<TInterface>
    where TInterface : class, IBaseItem
    where TItem : BaseItem, TInterface
{
    /// <summary>
    /// The method to create a command to delete the item.
    /// </summary>
    private Func<TItem, ISaveCommand<TInterface>> _convertToDeleteCommand = null!;

    /// <summary>
    /// The method to create a command to update the item.
    /// </summary>
    private Func<TItem, ISaveCommand<TInterface>> _convertToUpdateCommand = null!;

    /// <summary>
    /// Create a proxy item over a item.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="validateAsyncDelegate">The action to validate the item.</param>
    /// <param name="convertToDeleteCommand">The method to create a <see cref="ISaveCommand{TInterface}" to delete the item.</param>
    /// <param name="convertToUpdateCommand">The method to create a <see cref="ISaveCommand{TInterface}" to update the item.</param>
    /// <returns>A proxy item as TInterface.</returns>
    public static QueryResult<TInterface, TItem> Create(
        TItem item,
        ValidateAsyncDelegate<TInterface, TItem> validateAsyncDelegate,
        Func<TItem, ISaveCommand<TInterface>> convertToDeleteCommand,
        Func<TItem, ISaveCommand<TInterface>> convertToUpdateCommand)
    {
        // create the proxy manager - need an item reference for the ItemProxy onInvoke delegate
        var proxyManager = new QueryResult<TInterface, TItem>
        {
            _item = item,
            _isReadOnly = true,
            _validateAsyncDelegate = validateAsyncDelegate,
            _convertToDeleteCommand = convertToDeleteCommand,
            _convertToUpdateCommand = convertToUpdateCommand,
        };

        // create the proxy
        var proxy = ItemProxy<TInterface, TItem>.Create(proxyManager.OnInvoke);

        // set our proxy
        proxyManager._proxy = proxy;

        // return the proxy manager
        return proxyManager;
    }

    /// <summary>
    /// Returns an <see cref="ISaveCommand{TInterface}"/> to delete the item.
    /// </summary>
    /// <returns>An <see cref="ISaveCommand{TInterface}"/> to delete the item.</returns>
    public ISaveCommand<TInterface> Delete()
    {
        return _convertToDeleteCommand(_item);
    }

    /// <summary>
    /// Returns an <see cref="ISaveCommand{TInterface}"/> to update the item.
    /// </summary>
    /// <returns>An <see cref="ISaveCommand{TInterface}"/> to update the item.</returns>
    public ISaveCommand<TInterface> Update()
    {
        return _convertToUpdateCommand(_item);
    }
}
