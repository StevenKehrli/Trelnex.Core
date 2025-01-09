using System.Collections;
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
    private readonly ReaderWriterLockSlim _lock = new();

    /// <summary>
    /// The in memory backing store;
    /// </summary>
    private InMemoryStore _store = new();

    /// <summary>
    /// Creates a item in the backing data store as an asynchronous operation.
    /// </summary>
    /// <param name="item">The item to create.</param>
    /// <param name="itemEvent">The <see cref="ItemEvent"> that represents information regarding the item and the caller that invoked the save method.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> representing request cancellation.</param>
    /// <returns>The item that was created.</returns>
    protected override async Task<TItem> CreateItemAsync(
        TItem item,
        ItemEvent<TItem> itemEvent,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // lock
            _lock.EnterWriteLock();

            // create in the backing store
            var created = _store.CreateItemAsync(item, itemEvent);

            // return
            return await Task.FromResult(created);
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
    protected override async Task<TItem?> ReadItemAsync(
        string id,
        string partitionKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // lock
            _lock.EnterReadLock();

            // read from the backing store
            var read = _store.ReadItem(id, partitionKey);

            // return
            return await Task.FromResult<TItem?>(read);
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
        try
        {
            // lock
            _lock.EnterWriteLock();

            // updated the item
            var updated = _store.UpdateItem(item, itemEvent);

            // return
            return await Task.FromResult(updated);
        }
        finally
        {
            // unlock
            _lock.ExitWriteLock();
        }
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
        var queryable = _store
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

            _store = new();
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

            return _store.GetEvents();
        }
        finally
        {
            _lock.ExitReadLock();
        }
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

    /// <summary>
    /// Represents an item or event that has been serialized to a json string.
    /// </summary>
    /// <typeparam name="T">The type of the item or event.</typeparam>
    private class BaseSerialized<T> where T : BaseItem
    {
        /// <summary>
        /// The json serializer options
        /// </summary>
        private static readonly JsonSerializerOptions _options = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        private string _jsonString = null!;
        private string _eTag = null!;

        /// <summary>
        /// Gets the resource.
        /// </summary>
        public T Resource
        {
            get
            {
                // deserialize to the resource
                var resource = JsonSerializer.Deserialize<T>(_jsonString, _options)!;

                // set the etag
                resource.ETag = _eTag;

                // return the resource
                return resource;
            }
        }

        /// <summary>
        /// Gets the ETag of the resource.
        /// </summary>
        public string ETag => _eTag;

        protected static TSerialized BaseCreate<TSerialized>(
            T resource) where TSerialized : BaseSerialized<T>, new()
        {
            // create an instance of TSerialized
            var serialized = new TSerialized()
            {
                // serialize to a json string
                _jsonString = JsonSerializer.Serialize(resource, _options),

                // create a new ETag
                _eTag = Guid.NewGuid().ToString(),
            };

            return serialized;
        }
    }

    /// <summary>
    /// Represents an item that has been serialized to a json string.
    /// </summary>
    /// <param name="jsonString">The serialized json string of the item.</param>
    /// <param name="eTag">The ETag of the item.</param>
    private class SerializedItem
        : BaseSerialized<TItem>
    {
        public static SerializedItem Create(
            TItem item) => BaseCreate<SerializedItem>(item);
    }

    /// <summary>
    /// Represents an event that has been serialized to a json string.
    /// </summary>
    private class SerializedEvent
        : BaseSerialized<ItemEvent<TItem>>
    {
        public static SerializedEvent Create(
            ItemEvent<TItem> itemEvent) => BaseCreate<SerializedEvent>(itemEvent);
    }

    private class InMemoryStore : IEnumerable<TItem>
    {
        /// <summary>
        /// The backing store of items
        /// </summary>
        private readonly Dictionary<string, SerializedItem> _items = [];

        /// <summary>
        /// The backing store of events
        /// </summary>
        private readonly List<SerializedEvent> _events = [];

        /// <summary>
        /// Creates a item in the backing data store.
        /// </summary>
        /// <param name="item">The item to create.</param>
        /// <param name="itemEvent">The <see cref="ItemEvent"> that represents information regarding the item and the caller that invoked the save method.</param>
        /// <returns>The item that was created.</returns>
        public TItem CreateItemAsync(
            TItem item,
            ItemEvent<TItem> itemEvent)
        {
            // get the item key
            var itemKey = InMemoryCommandProvider<TInterface, TItem>.GetItemKey(item);

            // serialize the item and add to the backing store
            var serializedItem = SerializedItem.Create(item);
            if (_items.TryAdd(itemKey, serializedItem) is false)
            {
                throw new CommandException(HttpStatusCode.Conflict);
            }

            // serialize the event and add to the backing store
            var serializedEvent = SerializedEvent.Create(itemEvent);
            _events.Add(serializedEvent);

            return serializedItem.Resource;
        }

        /// <summary>
        /// Reads a item from the backing data store.
        /// </summary>
        /// <param name="id">The id of the item.</param>
        /// <param name="partitionKey">The partition key of the item.</param>
        /// <returns>The item that was read.</returns>
        public TItem? ReadItem(
            string id,
            string partitionKey)
        {
            // get the item key
            var itemKey = GetItemKey(
                partitionKey: partitionKey,
                id: id);

            // get the item
            if (_items.TryGetValue(itemKey, out var serializedItem) is false)
            {
                // not found
                return null;
            }

            // return
            return serializedItem.Resource;
        }

        /// <summary>
        /// Updates an item in the backing data store.
        /// </summary>
        /// <param name="item">The item to update.</param>
        /// <param name="itemEvent">The <see cref="ItemEvent"> that represents information regarding the item and the caller that invoked the save method.</param>
        /// <returns>The item that was updated.</returns>
        public TItem UpdateItem(
            TItem item,
            ItemEvent<TItem> itemEvent)
        {
            // get the item key
            var itemKey = InMemoryCommandProvider<TInterface, TItem>.GetItemKey(item);

            // get the item
            if (_items.TryGetValue(itemKey, out var serializedItem) is false)
            {
                // not found
                throw new CommandException(HttpStatusCode.NotFound);
            }

            // check the version (ETag) is unchanged
            if (string.Equals(serializedItem.ETag, item.ETag) is false)
            {
                throw new CommandException(HttpStatusCode.Conflict);
            }

            // serialize the item and update in the backing store
            serializedItem = SerializedItem.Create(item);
            _items[itemKey] = serializedItem;

            // serialize the event and add to the backing store
            var serializedEvent = SerializedEvent.Create(itemEvent);
            _events.Add(serializedEvent);

            // return
            return serializedItem.Resource;
        }

        public ItemEvent<TItem>[] GetEvents()
        {
            return _events.Select(se => se.Resource).ToArray();
        }

        public IEnumerator<TItem> GetEnumerator()
        {
            foreach (var serializedItem in _items.Values)
            {
                yield return serializedItem.Resource;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
