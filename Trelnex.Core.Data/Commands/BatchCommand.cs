using System.Net;
using FluentValidation.Results;

namespace Trelnex.Core.Data;

/// <summary>
/// The interface to save a batch of items in the backing data store.
/// </summary>
/// <typeparam name="TInterface">The interface type of the items in the backing data store.</typeparam>
public interface IBatchCommand<TInterface>
    where TInterface : class, IBaseItem
{
    /// <summary>
    /// Adds a <see cref="ISaveCommand{TInterface}"/> to the batch.
    /// </summary>
    /// <param name="saveCommand">>The <see cref="ISaveCommand{TInterface}"/> to add to the batch.</param>
    /// <returns>The <see cref="IBatchCommand{TInterface}"/> with the <see cref="ISaveCommand{TInterface}"/> added.</returns>
    IBatchCommand<TInterface> Add(
        ISaveCommand<TInterface> saveCommand);

    /// <summary>
    /// The action to save the batch of items to the backing data store as an asynchronous operation.
    /// </summary>
    /// <param name="requestContext">The <see cref="IRequestContext"> that invoked this method.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>The item that was saved.</returns>
    Task<IReadResult<TInterface>[]> SaveAsync(
        IRequestContext requestContext,
        CancellationToken cancellationToken);

    /// <summary>
    /// The action to validate the batch of items.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>The fluent <see cref="ValidationResult"/> item that was saved.</returns>
    Task<ValidationResult[]> ValidateAsync(
        CancellationToken cancellationToken);
}

/// <summary>
/// The class to save a batch of items in the backing data store.
/// </summary>
/// <typeparam name="TInterface">The interface type of the items in the backing data store.</typeparam>
internal class BatchCommand<TInterface, TItem>(
    SaveBatchAsyncDelegate<TInterface, TItem> saveBatchAsyncDelegate)
    : IBatchCommand<TInterface>
    where TInterface : class, IBaseItem
    where TItem : BaseItem, TInterface
{
    /// <summary>
    /// An exclusive lock to ensure that only one operation that modifies the batch is in progress at a time
    /// </summary>
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <summary>
    /// The batch of items to save.
    /// </summary>
    private readonly List<SaveCommand<TInterface, TItem>> _saveCommands = [];

    public IBatchCommand<TInterface> Add(
        ISaveCommand<TInterface> saveCommand)
    {
        // ensure that the save command is of the correct type
        if (saveCommand is not SaveCommand<TInterface, TItem> saveCommandImpl)
        {
            throw new ArgumentException(
                $"The {nameof(saveCommand)} must be of type {typeof(SaveCommand<TInterface, TItem>).Name}.",
                nameof(saveCommand));
        }

        // ensure that only one operation that modifies the batch is in progress at a time
        _semaphore.Wait();

        // add the save command to the batch
        _saveCommands.Add(saveCommandImpl);

        // release the exclusive lock
        _semaphore.Release();

        return this;
    }

    public async Task<IReadResult<TInterface>[]> SaveAsync(
        IRequestContext requestContext,
        CancellationToken cancellationToken)
    {
        // ensure that only one operation that modifies the batch is in progress at a time
        _semaphore.Wait();

        try
        {
            if (_saveCommands.Count is 0)
            {
                return [];
            }

            // get the batch items
            var batchItems = new BatchItem<TInterface, TItem>[_saveCommands.Count];

            // iterate over the save commands
            for (var index = 0; index < _saveCommands.Count; index++)
            {
                var saveCommand = _saveCommands[index];

                // start the batch operation
                batchItems[index] = await saveCommand.StartBatchAsync(requestContext, cancellationToken);
            }

            // get the distinct partition keys
            var partitionKeys = batchItems
                .Select(i => i.Item.PartitionKey)
                .Distinct()
                .ToArray();

            if (partitionKeys.Length is not 1)
            {
                throw new CommandException(HttpStatusCode.BadRequest, "The PartitionKey provided do not match.");
            }

            // save the batch
            var items = await saveBatchAsyncDelegate(
                partitionKey: partitionKeys[0],
                batchItems: batchItems,
                cancellationToken: cancellationToken);

            // allocate the results
            var readResults = new IReadResult<TInterface>[batchItems.Length];

            // iterate over the save commands
            for (var index = 0; index < _saveCommands.Count; index++)
            {
                var saveCommand = _saveCommands[index];

                // finalize the batch operation
                readResults[index] = saveCommand.FinalizeBatch(items[index]);
            }

            // clear the batch
            _saveCommands.Clear();

            return readResults;
        }
        catch
        {
            // iterate over the save commands
            for (var index = 0; index < _saveCommands.Count; index++)
            {
                var saveCommand = _saveCommands[index];

                // discard the batch operation
                saveCommand.DiscardBatch();
            }

            throw;
        }
        finally
        {
            // release the exclusive lock
            _semaphore.Release();
        }
    }

    public async Task<ValidationResult[]> ValidateAsync(CancellationToken cancellationToken)
    {
        // ensure that only one operation that modifies the batch is in progress at a time
        _semaphore.Wait();

        // allocate the results
        var validationResults = new ValidationResult[_saveCommands.Count];

        // iterate over the save commands
        for (var index = 0; index < _saveCommands.Count; index++)
        {
            var saveCommand = _saveCommands[index];

            // validate
            validationResults[index] = await saveCommand.ValidateAsync(cancellationToken);
        }

        // release the exclusive lock
        _semaphore.Release();

        return validationResults;
    }
}
