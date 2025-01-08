using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;

namespace Trelnex.Core.Data;

/// <summary>
/// An implementation of <see cref="ICommandProvider{TInterface, TItem}"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is a temporary store in memory for item storage and retrieval.
/// </para>
/// <para>
/// This command provider will serialize the item to a string for storage and deserialize the string to a item for retrieval.
/// This validates that the item is json attributed correctly for a persistent backing store (Cosmos).
/// </para>
/// </remarks>
internal class InMemoryCommandProvider<TInterface, TItem>(
    string typeName,
    AbstractValidator<TItem>? itemValidator = null,
    CommandOperations? commandOperations = null)
    : CommandProvider<TInterface, TItem>(typeName, itemValidator, commandOperations)
    where TInterface : class, IBaseItem
    where TItem : BaseItem, TInterface, new()
{
    /// <summary>
    /// An exclusive lock to ensure that only one operation that modifies the backing store is in progress at a time
    /// </summary>
    private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

    /// <summary>
    /// The backing store of items
    /// </summary>
    private Dictionary<string, TItem> _items = [];

    /// <summary>
    /// The backing store of events
    /// </summary>
    private List<ItemEvent<TItem>> _events = [];

    /// <summary>
    /// The json serializer options
    /// </summary>
    private static readonly JsonSerializerOptions _options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Creates a item in the backing data store as an asynchronous operation.
    /// </summary>
    /// <param name="item">The item to create.</param>
    /// <param name="itemEvent">The <see cref="ItemEvent"> that represents information regarding the item and the caller that invoked the save method.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> representing request cancellation.</param>
    /// <returns>The item that was created.</returns>
    protected override Task<TItem> CreateItemAsync(
        TItem item,
        ItemEvent<TItem> itemEvent,
        CancellationToken cancellationToken = default)
    {
        // get the item key
        var itemKey = InMemoryCommandProvider<TInterface, TItem>.GetItemKey(item);

        // process the item
        var processedItem = Process(item);

        // process the event
        var processedItemEvent = Process(itemEvent);

        try
        {
            // lock
            _lock.EnterWriteLock();

            // add to the backing store
            if (_items.TryAdd(itemKey, processedItem) is false)
            {
                throw new CommandException(HttpStatusCode.Conflict);
            }

            _events.Add(processedItemEvent);

            // return
            return Task.FromResult(processedItem);
        }
        finally
        {
            // unlock
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Reads a item from the backing data store as an asynchronous operation.
    /// </summary>
    /// <param name="id">The id of the item.</param>
    /// <param name="partitionKey">The partition key of the item.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> representing request cancellation.</param>
    /// <returns>The item that was read.</returns>
    protected override Task<TItem?> ReadItemAsync(
        string id,
        string partitionKey,
        CancellationToken cancellationToken = default)
    {
        // get the item key
        var itemKey = GetItemKey(
            partitionKey: partitionKey,
            id: id);

        try
        {
            // lock
            _lock.EnterReadLock();

            // get the item
            if (_items.TryGetValue(itemKey, out var currentItem) is false)
            {
                // not found
                return Task.FromResult<TItem?>(null);
            }

            // clone the item and return
            return Task.FromResult<TItem?>(result: Clone(currentItem));
        }
        finally
        {
            // unlock
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Updates a item in the backing data store as an asynchronous operation.
    /// </summary>
    /// <param name="item">The item to update.</param>
    /// <param name="itemEvent">The <see cref="ItemEvent"> that represents information regarding the item and the caller that invoked the save method.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> representing request cancellation.</param>
    /// <returns>The item that was updated.</returns>
    protected override async Task<TItem> UpdateItemAsync(
        TItem item,
        ItemEvent<TItem> itemEvent,
        CancellationToken cancellationToken = default)
    {
        // get the item key
        var itemKey = InMemoryCommandProvider<TInterface, TItem>.GetItemKey(item);

        // process the item
        var processedItem = Process(item);

        // process the event
        var processedItemEvent = Process(itemEvent);

        try
        {
            // lock
            _lock.EnterWriteLock();

            // get the item
            if (_items.TryGetValue(itemKey, out var currentItem) is false)
            {
                // not found
                throw new CommandException(HttpStatusCode.NotFound);
            }

            // check the version (ETag) is unchanged
            if (string.Equals(currentItem.ETag, item.ETag) is false)
            {
                throw new CommandException(HttpStatusCode.Conflict);
            }

            // update in the backing store
            _items[itemKey] = processedItem;

            _events.Add(processedItemEvent);

            // return
            return await Task.FromResult(processedItem);
        }
        finally
        {
            // unlock
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Saves a batch of items in the backing data store as an asynchronous operation.
    /// </summary>
    /// <param name="partitionKey">The partition key of the batch.</param>
    /// <param name="batchItems">The batch of items to save.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> representing request cancellation.</param>
    /// <returns>The items that were updated.</returns>
    protected override async Task<TItem[]> SaveBatchAsync(
        string partitionKey,
        BatchItem<TInterface, TItem>[] batchItems,
        CancellationToken cancellationToken = default)
    {
        // lock
        _lock.EnterWriteLock();

        // backup the backing store
        var itemsBackup = _items.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value);
        
        var eventsBackup = _events.ToList();

        // save the batch items
        var saveResults = new SaveResult[batchItems.Length];

        for (int index = 0; index < batchItems.Length; index++)
        {
            var batchItem = batchItems[index];

            try
            {
                // save
                var updatedItem = await Save(
                    batchItem: batchItem,
                    cancellationToken: cancellationToken);

                saveResults[index] = new SaveResult(
                    Item: updatedItem,
                    Exception: null);
            }
            catch (CommandException ex)
            {
                saveResults[index] = new SaveResult(
                    Item: null,
                    Exception: ex);
            }
        }

        // check for any failures
        var exceptions = saveResults
            .Where(r => r.Exception is not null)
            .Select(r => r.Exception!)
            .ToArray();

        if (exceptions.Length is not 0)
        {
            // reset the backing store
            _items = itemsBackup;
            _events = eventsBackup;

            throw new CommandException(
                httpStatusCode: HttpStatusCode.BadRequest,
                innerException: new AggregateException(exceptions));
        }

        // get the items and return
        var updatedItems = new TItem[batchItems.Length];

        for (var index = 0; index < batchItems.Length; index++)
        {
            updatedItems[index] = saveResults[index].Item!;
        }

        // unlock
        _lock.ExitWriteLock();

        return await Task.FromResult(updatedItems);
    }

    /// <summary>
    /// Create an instance of the <see cref="IQueryCommand{Interface}"/>.
    /// </summary>
    /// <param name="expressionConverter">The <see cref="ExpressionConverter{TInterface,TItem}"/> to convert an expression using a TInterface to an expression using a TItem.</param>
    /// <param name="convertToQueryResult">The method to convert a TItem to a <see cref="IQueryResult{TInterface}"/>.</param>
    /// <returns>The <see cref="IQueryCommand{Interface}"/>.</returns>
    protected override IQueryCommand<TInterface> CreateQueryCommand(
        ExpressionConverter<TInterface, TItem> expressionConverter,
        Func<TItem, IQueryResult<TInterface>> convertToQueryResult)
    {
        // deferred execution, so do not need to lock
        var queryable = _items.Values
            .AsQueryable()
            .Where(i => i.TypeName == TypeName)
            .Where(i => i.IsDeleted == null || i.IsDeleted == false);

        return new InMemoryQueryCommand(
            expressionConverter: expressionConverter,
            queryableLock: _lock,
            queryable: queryable,
            convertToQueryResult: convertToQueryResult);
    }

    internal void Clear()
    {
        try
        {
            _lock.EnterWriteLock();

            _items = [];
            _events = [];
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    internal ItemEvent<TItem>[] GetEvents()
    {
        try
        {
            _lock.EnterReadLock();

            return _events.ToArray();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    private async Task<TItem> Save(
        BatchItem<TInterface, TItem> batchItem,
        CancellationToken cancellationToken)
    {
        switch (batchItem.SaveAction)
        {
            case SaveAction.CREATED:
                return await CreateItemAsync(
                    item: batchItem.Item,
                    itemEvent: batchItem.ItemEvent,
                    cancellationToken: cancellationToken);

            case SaveAction.UPDATED:
            case SaveAction.DELETED:
                return await UpdateItemAsync(
                    item: batchItem.Item,
                    itemEvent: batchItem.ItemEvent,
                    cancellationToken: cancellationToken);

            default:
                throw new InvalidOperationException();
        }
    }

    private static T Clone<T>(
        T baseItem) where T : BaseItem
    {
        // serialize to json string
        var jsonString = JsonSerializer.Serialize(baseItem, _options);

        // deserialize back to type
        return JsonSerializer.Deserialize<T>(jsonString)!;
    }

    private static string GetItemKey(
        BaseItem item)
    {
        return GetItemKey(
            partitionKey: item.PartitionKey,
            id: item.Id);
    }

    private static string GetItemKey(
        string partitionKey,
        string id)
    {
        return $"{partitionKey}:{id}";
    }

    private static T Process<T>(
        T baseItem) where T : BaseItem
    {
        // clone
        var clone = Clone(baseItem);

        // set a new etag
        clone.ETag = Guid.NewGuid().ToString();

        return clone;
    }

    private record SaveResult(
        TItem? Item,
        CommandException? Exception);

    private class InMemoryQueryCommand(
        ExpressionConverter<TInterface, TItem> expressionConverter,
        ReaderWriterLockSlim queryableLock,
        IQueryable<TItem> queryable,
        Func<TItem, IQueryResult<TInterface>> convertToQueryResult)
        : QueryCommand<TInterface, TItem>(expressionConverter, queryable)
    {
        /// <summary>
        /// Execute the underlying <see cref="IQueryable{TItem}"/> and return the results as an async enumerable.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
        /// <returns>The <see cref="IAsyncEnumerable{TInterface}"/>.</returns>
        protected override async IAsyncEnumerable<IQueryResult<TInterface>> ExecuteAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            try
            {
                // lock
                queryableLock.EnterReadLock();

                foreach (var item in GetQueryable().AsEnumerable())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    yield return await Task.FromResult(convertToQueryResult(item));
                }
            }
            finally
            {
                // unlock
                queryableLock.ExitReadLock();
            }
        }
    }
}
