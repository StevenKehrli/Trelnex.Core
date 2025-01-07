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
}

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
internal class InMemoryCommandProvider<TInterface, TItem>
    : CommandProvider<TInterface, TItem>
    where TInterface : class, IBaseItem
    where TItem : BaseItem, TInterface, new()
{
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
        var processedItem = Process(item);

        // get the item key
        var itemKey = InMemoryCommandProvider<TInterface, TItem>.GetItemKey(processedItem);

        // add to the backing store
        if (_items.TryAdd(itemKey, processedItem) is false)
        {
            throw new CommandException(HttpStatusCode.Conflict);
        }



        // process the event
        var processedItemEvent = Process(itemEvent);

        // add to the backing store
        _events.Add(processedItemEvent);



        // return
        return Task.FromResult(processedItem);
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

        // clone the item and return
        var clone = Clone(item);
        return Task.FromResult<TItem?>(clone);
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
        var processedItem = Process(item);

        // get the item key
        var itemKey = InMemoryCommandProvider<TInterface, TItem>.GetItemKey(item);

        // update in the backing store
        _items[itemKey] = processedItem;



        // process the event
        var processedItemEvent = Process(itemEvent);

        // add to the backing store
        _events.Add(processedItemEvent);



        // return
        return processedItem;
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
        var queryable = _items.Values
            .AsQueryable()
            .Where(i => i.TypeName == TypeName)
            .Where(i => i.IsDeleted == null || i.IsDeleted == false);

        return new InMemoryQueryCommand(
            expressionConverter: expressionConverter,
            queryable: queryable,
            convertToQueryResult: convertToQueryResult);
    }

    internal ItemEvent<TItem>[] GetEvents()
    {
        return _events.ToArray();
    }

    private static T Clone<T>(
        T baseItem) where T : BaseItem
    {
        // serialize to json string
        var jsonString = JsonSerializer.Serialize(baseItem, _options);

        // deserialize back to type
        return JsonSerializer.Deserialize<T>(jsonString)!;
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
            foreach (var item in GetQueryable().AsEnumerable())
            {
                cancellationToken.ThrowIfCancellationRequested();

                yield return await Task.FromResult(convertToQueryResult(item));
            }
        }
    }
}
