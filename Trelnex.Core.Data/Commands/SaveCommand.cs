using FluentValidation.Results;

namespace Trelnex.Core.Data;

/// <summary>
/// The interface to validate and save the item in the backing data store.
/// </summary>
/// <typeparam name="TInterface">The interface type of the items in the backing data store.</typeparam>
public interface ISaveCommand<TInterface>
    where TInterface : class, IBaseItem
{
    /// <summary>
    /// The item.
    /// </summary>
    TInterface Item { get; }

    /// <summary>
    /// The action to save the item to the backing data store as an asynchronous operation.
    /// </summary>
    /// <param name="requestContext">The <see cref="IRequestContext"> that invoked this method.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>The item that was saved.</returns>
    Task<ISaveResult<TInterface>> SaveAsync(
        IRequestContext requestContext,
        CancellationToken cancellationToken);

    /// <summary>
    /// The action to validate the item.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>The fluent <see cref="ValidationResult"/> item that was saved.</returns>
    Task<ValidationResult> ValidateAsync(
        CancellationToken cancellationToken);
}

/// <summary>
/// The class to save the item in the backing data store.
/// </summary>
/// <typeparam name="TInterface">The interface type of the items in the backing data store.</typeparam>
internal class SaveCommand<TInterface, TItem>
    : ProxyManager<TInterface, TItem>, ISaveCommand<TInterface>
    where TInterface : class, IBaseItem
    where TItem : BaseItem, TInterface
{
    /// <summary>
    /// The type of save action.
    /// </summary>
    private SaveAction _saveAction;

    /// <summary>
    /// The action to save the item.
    /// </summary>
    private SaveAsyncDelegate<TInterface, TItem> _saveAsyncDelegate = null!;

    /// <summary>
    /// Create a proxy item over a item.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="isReadOnly">Indicates if the item is read-only.</param>
    /// <param name="saveAction">The type of save action.</param>
    /// <param name="saveAsyncDelegate">The action to save the item.</param>
    /// <param name="validateAsyncDelegate">The action to validate the item.</param>
    /// <returns>A proxy item as TInterface.</returns>
    public static SaveCommand<TInterface, TItem> Create(
        TItem item,
        bool isReadOnly,
        ValidateAsyncDelegate<TInterface, TItem> validateAsyncDelegate,
        SaveAction saveAction,
        SaveAsyncDelegate<TInterface, TItem> saveAsyncDelegate)
    {
        // create the proxy manager - need an item reference for the ItemProxy onInvoke delegate
        var proxyManager = new SaveCommand<TInterface, TItem>
        {
            _item = item,
            _isReadOnly = isReadOnly,
            _validateAsyncDelegate = validateAsyncDelegate,
            _saveAction = saveAction,
            _saveAsyncDelegate = saveAsyncDelegate,
        };

        // create the proxy
        var proxy = ItemProxy<TInterface, TItem>.Create(proxyManager.OnInvoke);

        // set our proxy
        proxyManager._proxy = proxy;

        // return the proxy manager
        return proxyManager;
    }

    /// <summary>
    /// The action to save the item.
    /// </summary>
    /// <param name="requestContext">The <see cref="IRequestContext"> that invoked this method.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="ISaveResult{TInterface}"/> representing the saved item.</returns>
    /// <exception cref="InvalidOperationException">The command is no longer valid.</exception>
    public async Task<ISaveResult<TInterface>> SaveAsync(
        IRequestContext requestContext,
        CancellationToken cancellationToken)
    {
        // check if already saved
        if (_saveAsyncDelegate is null)
        {
            throw new InvalidOperationException("The Command is no longer valid because its SaveAsync method has already been called.");
        }

        // validate the underlying item
        var validationResult = await ValidateAsync(cancellationToken);

        validationResult.ValidateOrThrow<TItem>();

        // create the event
        var itemEvent = ItemEvent<TItem>.Create(
            related: _item,
            saveAction: _saveAction,
            changes: GetPropertyChanges(),
            requestContext: requestContext);

        // save the item - get the updated item
        var updatedItem = await _saveAsyncDelegate(
            item: _item,
            itemEvent: itemEvent,
            cancellationToken: cancellationToken);

        // create the updated proxy over the updated item
        var updatedProxy = ItemProxy<TInterface, TItem>.Create(OnInvoke);

        // set the updated item and proxy
        _item = updatedItem;
        _proxy = updatedProxy;
        _isReadOnly = true;

        // null out the saveAsyncDelegate so we know that we have already saved are are no longer valid
        _saveAsyncDelegate = null!;

        // create the read result and return
        return SaveResult<TInterface, TItem>.Create(
            item: updatedItem,
            validateAsyncDelegate: _validateAsyncDelegate);
    }
}
