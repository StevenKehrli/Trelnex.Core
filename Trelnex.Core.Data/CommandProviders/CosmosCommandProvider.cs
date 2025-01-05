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
        if (string.Equals(item.PartitionKey, itemEvent.PartitionKey) is false)
        {
            throw new CommandException(HttpStatusCode.BadRequest, "The PartitionKey provided do not match.");
        }

        // create the batch operation
        var partitionKey = new PartitionKey(item.PartitionKey);
        var batch = container.CreateTransactionalBatch(partitionKey);

        // item
        batch.CreateItem(item);

        // event
        batch.CreateItem(itemEvent);

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
        if (string.Equals(item.PartitionKey, itemEvent.PartitionKey) is false)
        {
            throw new CommandException(HttpStatusCode.BadRequest, "The PartitionKey provided do not match.");
        }

        // create the batch operation
        var partitionKey = new PartitionKey(item.PartitionKey);
        var batch = container.CreateTransactionalBatch(partitionKey);

        // item
        var requestOptions = new TransactionalBatchItemRequestOptions
        {
            IfMatchEtag = item.ETag
        };

        batch.ReplaceItem(
            id: item.Id,
            item: item,
            requestOptions: requestOptions);

        // event
        batch.CreateItem(itemEvent);

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
    /// Create an instance of the <see cref="IQueryCommand{Interface}"/>.
    /// </summary>
    /// <param name="expressionConverter">The <see cref="ExpressionConverter{TInterface,TItem}"/> to convert an expression using a TInterface to an expression using a TItem.</param>
    /// <param name="convertToReadResult">The method to convert a TItem to a <see cref="IReadResult{TInterface}"/>.</param>
    /// <returns>The <see cref="IQueryCommand{Interface}"/>.</returns>
    protected override IQueryCommand<TInterface> CreateQueryCommand(
        ExpressionConverter<TInterface, TItem> expressionConverter,
        Func<TItem, IReadResult<TInterface>> convertToReadResult)
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
            convertToReadResult: convertToReadResult);
    }

    private class CosmosQueryCommand(
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
                    yield return convertToReadResult(item);
                }
            }
        }
    }
}
