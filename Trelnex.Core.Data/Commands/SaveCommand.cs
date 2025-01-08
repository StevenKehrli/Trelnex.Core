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
    Task<IReadResult<TInterface>> SaveAsync(
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
    /// <param name="validateAsyncDelegate">The action to validate the item.</param>
    /// <param name="saveAction">The type of save action.</param>
    /// <param name="saveAsyncDelegate">The action to save the item.</param>
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
    /// <returns>A <see cref="IReadResult{TInterface}"/> representing the saved item.</returns>
    /// <exception cref="InvalidOperationException">The command is no longer valid.</exception>
    public async Task<IReadResult<TInterface>> SaveAsync(
        IRequestContext requestContext,
        CancellationToken cancellationToken)
    {
        // start the batch operation and get the batch item
        var batchItem = await StartBatchAsync(requestContext, cancellationToken);

        try
        {
            // save the item - get the updated item
            var updatedItem = await _saveAsyncDelegate(
                item: batchItem.Item,
                itemEvent: batchItem.ItemEvent,
                cancellationToken: cancellationToken);

            // finalize the batch operation
            return FinalizeBatch(updatedItem);
        }
        catch
        {
            // discard the batch operation
            DiscardBatch();

            // rethrow the exception
            throw;
        }
    }

    /// <summary>
    /// Start the batch operation.
    /// </summary>
    /// <param name="requestContext">The <see cref="IRequestContext"> that invoked this method.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="BatchItem{TInterface, TItem}"/> to add to the batch.</returns>
    /// <exception cref="InvalidOperationException">The command is no longer valid.</exception>
    internal async Task<BatchItem<TInterface, TItem>> StartBatchAsync(
        IRequestContext requestContext,
        CancellationToken cancellationToken)
    {
        // ensure that only one operation that modifies the item is in progress at a time
        await _semaphore.WaitAsync(cancellationToken);

        try
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

            return new BatchItem<TInterface, TItem>(
                Item: _item,
                ItemEvent: itemEvent);
        }
        catch
        {
            // release the exclusive lock
            _semaphore.Release();

            // rethrow the exception
            throw;
        }
    }

    /// <summary>
    /// Finalize the batch operation.
    /// </summary>
    /// <param name="updatedProxy">The updated item returned from the batch.</param>
    /// <returns>A <see cref="IReadResult{TInterface}"/> representing the updated item.</returns>
    internal IReadResult<TInterface> FinalizeBatch(
        TItem updatedItem)
    {
        // create the updated proxy over the updated item
        var updatedProxy = ItemProxy<TInterface, TItem>.Create(OnInvoke);

        // set the updated item and proxy
        _item = updatedItem;
        _proxy = updatedProxy;
        _isReadOnly = true;

        // null out the saveAsyncDelegate so we know that we have already saved and are no longer valid
        _saveAsyncDelegate = null!;

        // release the exclusive lock
        _semaphore.Release();

        // create the read result and return
        return ReadResult<TInterface, TItem>.Create(
            item: updatedItem,
            validateAsyncDelegate: _validateAsyncDelegate);
    }

    /// <summary>
    /// Discard the batch operation.
    /// </summary>
    internal void DiscardBatch()
    {
        // release the exclusive lock
        _semaphore.Release();
    }
}
