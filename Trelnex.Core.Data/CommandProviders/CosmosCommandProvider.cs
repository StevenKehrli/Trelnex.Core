using System.Net;
using System.Runtime.CompilerServices;
using FluentValidation;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;

namespace Trelnex.Core.Data;

/// <summary>
/// An implementation of <see cref="ICommandProvider{TInterface}"/> that uses a CosmosDB container as a backing store.
/// </summary>
internal class CosmosCommandProvider<TInterface, TItem>(
    Container container,
    string typeName,
    AbstractValidator<TItem>? validator = null,
    CommandOperations? commandOperations = null)
    : CommandProvider<TInterface, TItem>(typeName, validator, commandOperations)
    where TInterface : class, IBaseItem
    where TItem : BaseItem, TInterface, new()
{
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
            return await container.ReadItemAsync<TItem>(
                id: id,
                partitionKey: new PartitionKey(partitionKey),
                cancellationToken: cancellationToken);
        }
        catch (CosmosException ce) when (ce.StatusCode == HttpStatusCode.NotFound)
        {
            return default;
        }
        catch (CosmosException ex)
        {
            throw new CommandException(ex.StatusCode, ex.Message);
        }
    }

    /// <summary>
    /// Saves a item in the backing data store as an asynchronous operation.
    /// </summary>
    /// <param name="saveContext">The context with item and event to save.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> representing request cancellation.</param>
    /// <returns>The item that was saved.</returns>
    protected override async Task<TItem> SaveItemAsync(
        SaveContext<TInterface, TItem> saveContext,
        CancellationToken cancellationToken = default)
    {
        var batch = container.CreateTransactionalBatch(
            new PartitionKey(saveContext.Item.PartitionKey));

        // add the item to the batch
        AddItem(batch, saveContext);

        try
        {
            // execute the batch
            using var response = await batch.ExecuteAsync(cancellationToken);

            // get the returned item and return it
            var itemResponse = response.GetOperationResultAtIndex<TItem>(0);

            // check the status code
            if (itemResponse.IsSuccessStatusCode is false)
            {
                throw new CommandException(itemResponse.StatusCode);
            }

            return itemResponse.Resource;
        }
        catch (CosmosException ce)
        {
            throw new CommandException(ce.StatusCode, ce.Message);
        }
    }

    /// <summary>
    /// Saves a batch of items in the backing data store as an asynchronous operation.
    /// </summary>
    /// <param name="partitionKey">The partition key of the batch.</param>
    /// <param name="saveContexts">The batch of contexts with item and event to save.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> representing request cancellation.</param>
    /// <returns>The results of the batch operation.</returns>
    protected override async Task<SaveResult<TInterface, TItem>[]> SaveBatchAsync(
        string partitionKey,
        SaveContext<TInterface, TItem>[] saveContexts,
        CancellationToken cancellationToken = default)
    {
        // allocate the results
        var saveResults = new SaveResult<TInterface, TItem>[saveContexts.Length];

        // create the batch operation
        var batch = container.CreateTransactionalBatch(
            new PartitionKey(partitionKey));

        // add the items to the batch
        for (int index = 0; index < saveContexts.Length; index++)
        {
            AddItem(batch, saveContexts[index]);
        }

        try
        {
            // execute the batch
            using var response = await batch.ExecuteAsync(cancellationToken);

            for (int index = 0; index < saveContexts.Length; index++)
            {
                // get the returned item
                var itemResponse = response.GetOperationResultAtIndex<TItem>(index);

                // check the status code and build the result
                var httpStatusCode = itemResponse.IsSuccessStatusCode
                    ? HttpStatusCode.OK
                    : itemResponse.StatusCode;

                var item = itemResponse.IsSuccessStatusCode
                    ? itemResponse.Resource
                    : null;

                saveResults[index] = new SaveResult<TInterface, TItem>(
                    httpStatusCode,
                    item);
            }

            return saveResults;
        }
        catch (CosmosException ce)
        {
            throw new CommandException(ce.StatusCode, ce.Message);
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
        // add typeName and isDeleted predicates
        // the lambda parameter i is an item of TInterface type
        var queryable = container
            .GetItemLinqQueryable<TItem>()
            .Where(i => i.TypeName == TypeName)
            .Where(i => i.IsDeleted.IsDefined() == false || i.IsDeleted == false);

        return new CosmosQueryCommand(
            expressionConverter: expressionConverter,
            queryable: queryable,
            convertToQueryResult: convertToQueryResult);
    }

    /// <summary>
    /// Add the item to the batch.
    /// </summary>
    /// <param name="batch">The <see cref="TransactionalBatch"/> to add the item to.</param>
    /// <param name="saveContext">The context with item and event to save.</param>
    /// <returns>The <see cref="TransactionalBatch"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the <see cref="SaveAction"/> is not recognized.</exception>
    private static TransactionalBatch AddItem(
        TransactionalBatch batch,
        SaveContext<TInterface, TItem> saveContext) => saveContext.SaveAction switch
    {
        SaveAction.CREATED => batch
            .CreateItem(
                item: saveContext.Item)
            .CreateItem(
                item: saveContext.Event),

        SaveAction.UPDATED or SaveAction.DELETED => batch
            .ReplaceItem(
                id: saveContext.Item.Id,
                item: saveContext.Item,
                requestOptions: new TransactionalBatchItemRequestOptions { IfMatchEtag = saveContext.Item.ETag })
            .CreateItem(
                item: saveContext.Event),

        _ => throw new InvalidOperationException($"Unrecognized SaveAction: {saveContext.SaveAction}")
    };

    private class CosmosQueryCommand(
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
            // get the feed iterator
            var feedIterator = GetQueryable().ToFeedIterator();

            while (feedIterator.HasMoreResults)
            {
                FeedResponse<TItem>? feedResponse = null;

                try
                {
                    // this is where cosmos will throw
                    feedResponse = await feedIterator.ReadNextAsync(cancellationToken);
                }
                catch (CosmosException ex)
                {
                    throw new CommandException(ex.StatusCode);
                }

                foreach (var item in feedResponse)
                {
                    yield return convertToQueryResult(item);
                }
            }
        }
    }
}
