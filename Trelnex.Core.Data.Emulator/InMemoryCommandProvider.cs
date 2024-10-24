using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;

namespace Trelnex.Core.Data;

public static class InMemoryCommandProvider
{
    /// <summary>
    /// Create an instance of the <see cref="InMemoryCommandProvider"/>.
    /// </summary>
    /// <param name="typeName">The type name of the item - used for <see cref="BaseItem.TypeName"/>.</param>
    /// <param name="validator">The fluent validator for the item</param>
    /// <param name="commandOperations">The value indicating if update and delete commands are allowed. By default, update is allowed; delete is not allowed.</param>
    /// <typeparam name="TInterface">The specified interface type></typeparam>
    /// <typeparam name="TItem">The specified item type that implements the specified interface type</typeparam>
    /// <returns>The <see cref="InMemoryCommandProvider"/>.</returns>
    public static ICommandProvider<TInterface> Create<TInterface, TItem>(
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

    /// <summary>
    /// Create an instance of the <see cref="InMemoryCommandProvider"/>.
    /// </summary>
    /// <param name="persistPath">The local path to persist the items.</param>
    /// <param name="typeName">The type name of the item - used for <see cref="BaseItem.TypeName"/>.</param>
    /// <param name="validator">The fluent validator for the item</param>
    /// <param name="commandOperations">The value indicating if update and delete commands are allowed. By default, update is allowed; delete is not allowed.</param>
    /// <typeparam name="TInterface">The specified interface type></typeparam>
    /// <typeparam name="TItem">The specified item type that implements the specified interface type</typeparam>
    /// <returns>The <see cref="InMemoryCommandProvider"/>.</returns>
    public static ICommandProvider<TInterface> Create<TInterface, TItem>(
        string persistPath,
        string typeName,
        AbstractValidator<TItem>? validator = null,
        CommandOperations? commandOperations = null)
        where TInterface : class, IBaseItem
        where TItem : BaseItem, TInterface, new()
    {
        return new InMemoryCommandProvider<TInterface, TItem>(
            persistPath,
            typeName,
            validator,
            commandOperations);
    }
}

/// <summary>
/// An implementation of <see cref="ICommandProvider{TInterface, TItem}"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is a temporary store in memory for item storage and retrieval. Items are optionally persisted to local storage.
/// </para>
/// <para>
/// This command provider will serialize the item to a string for storage and deserialize the string to a item for retrieval.
/// This validates that the item is json attributed correctly for a persistent backing store (Cosmos).
/// </para>
/// </remarks>
internal class InMemoryCommandProvider<TInterface, TItem>
    : CommandProvider<TInterface, TItem>
    where TInterface : class, IBaseItem
    where TItem : BaseItem, TInterface, new()
{
    /// <summary>
    /// The local path to persist the items
    /// </summary>
    private readonly string? _persistPath = null;

    /// <summary>
    /// The backing store of items
    /// </summary>
    private readonly Dictionary<string, TItem> _items = [];

    /// <summary>
    /// The backing store of events
    /// </summary>
    private readonly List<ItemEvent<TItem>> _events = [];

    /// <summary>
    /// The json serializer options
    /// </summary>
    private static readonly JsonSerializerOptions _options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public InMemoryCommandProvider(
        string typeName,
        AbstractValidator<TItem>? itemValidator = null,
        CommandOperations? commandOperations = null)
        : base(typeName, itemValidator, commandOperations)
    {
    }

    public InMemoryCommandProvider(
        string persistPath,
        string typeName,
        AbstractValidator<TItem>? itemValidator = null,
        CommandOperations? commandOperations = null)
        : base(typeName, itemValidator, commandOperations)
    {
        _persistPath = persistPath;

        _items = LoadItemsFromLocalStorage(persistPath, typeName);
        _events = LoadEventsFromLocalStorage(persistPath, typeName);
    }

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
        // process the item
        var updatedItem = ProcessItem(item);

        // get the item key
        var itemKey = InMemoryCommandProvider<TInterface, TItem>.GetItemKey(updatedItem);

        // add to the backing store
        if (_items.TryAdd(itemKey, updatedItem) is false)
        {
            throw new HttpStatusCodeException(HttpStatusCode.Conflict);
        }



        // process the event
        var updatedItemEvent = ProcessEvent(itemEvent);

        // add to the backing store
        _events.Add(updatedItemEvent);



        // persist to local storage
        SaveToLocalStorage();

        // return
        return Task.FromResult(updatedItem);
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

        // get the item
        if (_items.TryGetValue(itemKey, out var item) is false)
        {
            // not found
            return Task.FromResult<TItem?>(null);
        }

        return Task.FromResult<TItem?>(item);
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
        // get the current item
        var currentItem = await ReadItemAsync(item.Id, item.PartitionKey, cancellationToken);

        if (currentItem is null) throw new CommandException(HttpStatusCode.NotFound);

        // check the version (ETag) is unchanged
        if (string.Equals(currentItem.ETag, item.ETag) is false)
        {
            throw new CommandException(HttpStatusCode.Conflict);
        }



        // process the item
        var updatedItem = ProcessItem(item);

        // get the item key
        var itemKey = InMemoryCommandProvider<TInterface, TItem>.GetItemKey(item);

        // update in the backing store
        _items[itemKey] = updatedItem;



        // process the event
        var updatedItemEvent = ProcessEvent(itemEvent);

        // add to the backing store
        _events.Add(updatedItemEvent);



        // persist to local storage
        SaveToLocalStorage();

        // return
        return updatedItem;
    }

    /// <summary>
    /// Create an instance of the <see cref="IQueryCommand{Interface}"/>.
    /// </summary>
    /// <param name="expressionConverter">The <see cref="ExpressionConverter{TInterface,TItem}"/> to convert an expression using a TInterface to an expression using a TItem.</param>
    /// <returns>The <see cref="IQueryCommand{Interface}"/>.</returns>
    protected override IQueryCommand<TInterface> CreateQueryCommand(
        ExpressionConverter<TInterface, TItem> expressionConverter)
    {
        var queryable = _items.Values
            .AsQueryable()
            .Where(i => i.TypeName == TypeName)
            .Where(i => i.IsDeleted == null || i.IsDeleted == false);

        return new InMemoryQueryCommand(
            expressionConverter: expressionConverter,
            queryable: queryable,
            convertToReadResult: CreateReadResult);
    }

    internal ItemEvent<TItem>[] GetEvents()
    {
        return _events.ToArray();
    }

    private static List<ItemEvent<TItem>> LoadEventsFromLocalStorage(
        string path,
        string typeName)
    {
        if (path is null) return [];

        // deserialize the array of TItem from the local file

        var eventPath = Path.Combine(path, $"{typeName}-events.json");

        if (Path.Exists(eventPath) is false) return [];

        using var stream = File.OpenRead(eventPath);

        var events = JsonSerializer.Deserialize<ItemEvent<TItem>[]>(stream)!;

        return events.ToList();
    }

    private static Dictionary<string, TItem> LoadItemsFromLocalStorage(
        string path,
        string typeName)
    {
        if (path is null) return [];

        // deserialize the array of TItem from the local file

        var itemPath = Path.Combine(path, $"{typeName}.json");

        if (Path.Exists(itemPath) is false) return [];

        using var stream = File.OpenRead(itemPath);

        var items = JsonSerializer.Deserialize<TItem[]>(stream)!;

        return items.ToDictionary(
            keySelector: item => GetItemKey(item),
            elementSelector: item => item);
    }

    private void SaveToLocalStorage()
    {
        if (_persistPath is null) return;

        // serialize the array of TItem to the local file

        var itemPath = Path.Combine(_persistPath, $"{_typeName}.json");

        using var itemStream = File.OpenWrite(itemPath);

        JsonSerializer.Serialize(itemStream, _items.Values, _options);

        // serialize the array of ItemEvent<TItem> to the local file

        var eventPath = Path.Combine(_persistPath, $"{_typeName}-events.json");

        using var eventStream = File.OpenWrite(eventPath);

        JsonSerializer.Serialize(eventStream, _events, _options);
    }

    private static ItemEvent<TItem> ProcessEvent(
        ItemEvent<TItem> itemEvent)
    {
        // set a new etag
        itemEvent.ETag = Guid.NewGuid().ToString();

        // serialize to a json string
        var eventJsonString = JsonSerializer.Serialize(itemEvent, _options);

        // deserialize to "updated' item
        var updatedItemEvent = JsonSerializer.Deserialize<ItemEvent<TItem>>(eventJsonString)!;

        // fix the dynamics

        return updatedItemEvent;
    }

    private static TItem ProcessItem(
        TItem item)
    {
        // set a new etag
        item.ETag = Guid.NewGuid().ToString();

        // serialize to json string
        var itemJsonString = JsonSerializer.Serialize(item, _options);

        // deserialize to "updated' item
        return JsonSerializer.Deserialize<TItem>(itemJsonString)!;
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
        IQueryable<TItem> queryable,
        Func<TItem, IReadResult<TInterface>> convertToReadResult)
        : QueryCommand<TInterface, TItem>(expressionConverter, queryable)
    {
        /// <summary>
        /// Execute the underlying <see cref="IQueryable{TItem}"/> and return the results as an async enumerable.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
        /// <returns>The <see cref="IAsyncEnumerable{TInterface}"/>.</returns>
        protected override async IAsyncEnumerable<IReadResult<TInterface>> ExecuteAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var item in GetQueryable().AsEnumerable())
            {
                cancellationToken.ThrowIfCancellationRequested();

                yield return await Task.FromResult(convertToReadResult(item));
            }
        }
    }
}
