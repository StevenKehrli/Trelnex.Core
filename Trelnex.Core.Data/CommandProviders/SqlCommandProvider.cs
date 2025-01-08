using System.Net;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Transactions;
using FluentValidation;
using LinqToDB;
using LinqToDB.Data;
using Microsoft.Azure.Cosmos;
using Microsoft.Data.SqlClient;

namespace Trelnex.Core.Data;

/// <summary>
/// An implementation of <see cref="ICommandProvider{TInterface}"/> that uses a SQL table as a backing store.
/// </summary>
internal partial class SqlCommandProvider<TInterface, TItem>(
    DataOptions dataOptions,
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

        // create the transaction
        using var transactionScope = new TransactionScope();

        // create the connection
        using var dataConnection = new DataConnection(dataOptions);

        try
        {
            // item
            dataConnection.Insert(item);

            // event
            dataConnection.Insert(itemEvent);

            // get the created item
            var created = dataConnection
                .GetTable<TItem>()
                .Where(i => i.Id == item.Id && i.PartitionKey == item.PartitionKey)
                .First();

            // commit the transaction
            transactionScope.Complete();

            return await Task.FromResult(created);
        }
        catch (SqlException se) when (PrimaryKeyViolationRegex().IsMatch(se.Message))
        {
            throw new CommandException(HttpStatusCode.Conflict);
        }
        catch (SqlException se)
        {
            throw new CommandException(HttpStatusCode.InternalServerError, se.Message);
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
            // create the connection
            using var dataConnection = new DataConnection(dataOptions);

            // get the item
            var item = dataConnection
                .GetTable<TItem>()
                .Where(i => i.Id == id && i.PartitionKey == partitionKey)
                .FirstOrDefault();

            return await Task.FromResult(item);
        }
        catch (CosmosException ce) when (ce.StatusCode == HttpStatusCode.NotFound)
        {
            return default;
        }
        catch (CosmosException ce)
        {
            throw new CommandException(ce.StatusCode, ce.Message);
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

        // create the transaction
        using var transactionScope = new TransactionScope();

        // create the connection
        using var dataConnection = new DataConnection(dataOptions);

        try
        {
            // item
            dataConnection.Update(item);

            // event
            dataConnection.Insert(itemEvent);

            // get the updated item
            var updated = dataConnection
                .GetTable<TItem>()
                .Where(i => i.Id == item.Id && i.PartitionKey == item.PartitionKey)
                .First();

            // commit the transaction
            transactionScope.Complete();

            return await Task.FromResult(updated);
        }
        catch (SqlException se) when (PreconditionFailedRegex().IsMatch(se.Message))
        {
            throw new CommandException(HttpStatusCode.PreconditionFailed);
        }
        catch (SqlException se)
        {
            throw new CommandException(HttpStatusCode.InternalServerError, se.Message);
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
        // create the transaction
        using var transactionScope = new TransactionScope();

        // create the connection
        using var dataConnection = new DataConnection(dataOptions);

        // save the batch items
        var saveResults = new SaveResult[batchItems.Length * 2];

        for (int index = 0; index < batchItems.Length; index++)
        {
            saveResults[index] = Save(
                dataConnection: dataConnection,
                item: batchItems[index].Item,
                saveAction: batchItems[index].SaveAction);

            saveResults[index + batchItems.Length] = Save(
                dataConnection: dataConnection,
                itemEvent: batchItems[index].ItemEvent);
        }

        // check for any failures
        var exceptions = saveResults
            .Where(r => r.IsSuccessStatusCode is false)
            .Select(r => new CommandException(r.StatusCode, r.Message))
            .ToArray();

        if (exceptions.Length is not 0)
        {
            throw new AggregateException(exceptions);
        }

        // get the items and return
        var updatedItems = new TItem[batchItems.Length];

        for (var index = 0; index < batchItems.Length; index++)
        {
            updatedItems[index] = saveResults[index].Item!;
        }

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
        // add typeName and isDeleted predicates
        // the lambda parameter i is an item of TInterface type
        var queryable = Enumerable.Empty<TItem>().AsQueryable()
            .Where(i => i.TypeName == TypeName)
            .Where(i => i.IsDeleted == null || i.IsDeleted == false);

        return new SqlQueryCommand(
            expressionConverter: expressionConverter,
            queryable: queryable,
            dataOptions: dataOptions,
            convertToQueryResult: convertToQueryResult);
    }

    /// <summary>
    /// Saves the batch item in the backing data store.
    /// </summary>
    /// <param name="dataConnection">The <see cref="DataConnection"/> to the backing data store.</param>
    /// <param name="item">The batch item to save.</param>
    /// <param name="saveAction">The save action of the batch item.</param>
    /// <returns>The <see cref="SqlResult"/> of the save operation.</returns>
    private static SaveResult Save(
        DataConnection dataConnection,
        TItem item,
        SaveAction saveAction)
    {
        try
        {
            switch (saveAction)
            {
                case SaveAction.CREATED:
                    dataConnection.Insert(item);
                    break;

                case SaveAction.UPDATED:
                case SaveAction.DELETED:
                    dataConnection.Update(item);
                    break;
            }

            // get the updated item
            var updatedItem = dataConnection
                .GetTable<TItem>()
                .Where(i => i.Id == item.Id && i.PartitionKey == item.PartitionKey)
                .First();

            return new SaveResult(
                StatusCode: HttpStatusCode.OK,
                IsSuccessStatusCode: true,
                Item: updatedItem,
                Message: null);
        }
        catch (SqlException se)
        {
            HttpStatusCode statusCode = HttpStatusCode.InternalServerError;

            if (PreconditionFailedRegex().IsMatch(se.Message))
            {
                statusCode = HttpStatusCode.PreconditionFailed;
            }
            else if (PrimaryKeyViolationRegex().IsMatch(se.Message))
            {
                statusCode = HttpStatusCode.Conflict;
            }

            return new SaveResult(
                StatusCode: statusCode,
                IsSuccessStatusCode: false,
                Item: null,
                Message: se.Message);
        }        
    }

    /// <summary>
    /// Saves the batch item event in the backing data store.
    /// </summary>
    /// <param name="dataConnection">The <see cref="DataConnection"/> to the backing data store.</param>
    /// <param name="itemEvent">The batch item event to save.</param>
    /// <returns>The <see cref="SaveResult"/> of the save operation.</returns>
    private static SaveResult Save(
        DataConnection dataConnection,
        ItemEvent<TItem> itemEvent)
    {
        try
        {
            dataConnection.Insert(itemEvent);

            return new SaveResult(
                StatusCode: HttpStatusCode.OK,
                IsSuccessStatusCode: true,
                Item: null,
                Message: null);
        }
        catch (SqlException se)
        {
            return new SaveResult(
                StatusCode: HttpStatusCode.InternalServerError,
                IsSuccessStatusCode: false,
                Item: null,
                Message: se.Message);
        }
    }

    private record SaveResult(
        HttpStatusCode StatusCode,
        bool IsSuccessStatusCode,
        TItem? Item,
        string? Message);

    private class SqlQueryCommand(
        ExpressionConverter<TInterface, TItem> expressionConverter,
        IQueryable<TItem> queryable,
        DataOptions dataOptions,
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
            // create the connection
            using var dataConnection = new DataConnection(dataOptions);

            // create the query from the table and the queryable expression
            var queryable = dataConnection
                .GetTable<TItem>()
                .Provider
                .CreateQuery<TItem>(GetQueryable().Expression);

            foreach (var item in queryable.AsEnumerable())
            {
                cancellationToken.ThrowIfCancellationRequested();

                yield return await Task.FromResult(convertToQueryResult(item));
            }
        }
    }

    [GeneratedRegex("^Precondition Failed\\.$")]
    private static partial Regex PreconditionFailedRegex();

    [GeneratedRegex("^Violation of PRIMARY KEY constraint ")]
    private static partial Regex PrimaryKeyViolationRegex();
}
