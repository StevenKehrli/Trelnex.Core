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
    /// <returns>The array of items that were saved.</returns>
    Task<IBatchResult<TInterface>[]> SaveAsync(
        IRequestContext requestContext,
        CancellationToken cancellationToken);

    /// <summary>
    /// The action to validate the batch of items.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>The array of fluent <see cref="ValidationResult"/> item that was saved.</returns>
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

    /// <summary>
    /// Adds a <see cref="ISaveCommand{TInterface}"/> to the batch.
    /// </summary>
    /// <param name="saveCommand">>The <see cref="ISaveCommand{TInterface}"/> to add to the batch.</param>
    /// <returns>The <see cref="IBatchCommand{TInterface}"/> with the <see cref="ISaveCommand{TInterface}"/> added.</returns>
    /// <exception cref="ArgumentException">if the <paramref name="saveCommand"/> is not of type <see cref="SaveCommand{TInterface, TItem}"/>.</exception>
    public IBatchCommand<TInterface> Add(
        ISaveCommand<TInterface> saveCommand)
    {
        // ensure that the save command is of the correct type
        if (saveCommand is not SaveCommand<TInterface, TItem> sc)
        {
            throw new ArgumentException(
                $"The {nameof(saveCommand)} must be of type {typeof(SaveCommand<TInterface, TItem>).Name}.",
                nameof(saveCommand));
        }

        // ensure that only one operation that modifies the batch is in progress at a time
        _semaphore.Wait();

        // add the save command to the batch
        _saveCommands.Add(sc);

        // release the exclusive lock
        _semaphore.Release();

        return this;
    }

    /// <summary>
    /// The action to save the batch of items to the backing data store as an asynchronous operation.
    /// </summary>
    /// <param name="requestContext">The <see cref="IRequestContext"> that invoked this method.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>The array of items that were saved.</returns>
    public async Task<IBatchResult<TInterface>[]> SaveAsync(
        IRequestContext requestContext,
        CancellationToken cancellationToken)
    {
        // ensure that only one operation that modifies the batch is in progress at a time
        _semaphore.Wait();

        try
        {
            // allocate the array of batch results
            var batchResults = new BatchResult<TInterface, TItem>[_saveCommands.Count];

            // validate the save commands
            var validationResultTasks = _saveCommands
                .Select(sc => sc.ValidateAsync(cancellationToken));

            var validationResults = await Task.WhenAll(validationResultTasks);
            validationResults.ValidateOrThrow<TItem>();

            // acquire the save requests from each save command
            var acquireTasks = _saveCommands
                .Select((sc, index) => {
                    return (
                        task: sc.AcquireAsync(requestContext, cancellationToken),
                        saveCommand: sc,
                        index: index);
                });

            // wait for the acquire tasks to complete
            // do not need to propagate the cancellation token
            // each acquire task will handle the cancellation
            Task.WaitAll(acquireTasks.Select(at => at.task), CancellationToken.None);

            var isFaulted = acquireTasks.Any(at => at.task.IsFaulted);

            if (isFaulted)
            {
                // something faulted

                foreach (var (task, saveCommand, index) in acquireTasks)
                {
                    // if this task completed successfully, release the save command
                    if (task.IsCompletedSuccessfully) saveCommand.Release();

                    // bad request: this faulted
                    // failed dependency: something else faulted
                    batchResults[index] = new BatchResult<TInterface, TItem>(
                        httpStatusCode: task.IsFaulted
                            ? HttpStatusCode.BadRequest
                            : HttpStatusCode.FailedDependency,
                        readResult: null);
                }

                return batchResults;
            }

            // throw if cancelled
            cancellationToken.ThrowIfCancellationRequested();

            // allocate the array of save requests
            var requests = new SaveRequest<TInterface, TItem>[_saveCommands.Count];

            foreach (var (task, saveCommand, index) in acquireTasks)
            {
                requests[index] = task.Result;
            }

            // save the batch
            var saveResults = await saveBatchAsyncDelegate(
                partitionKey: null!,
                requests: requests,
                cancellationToken: cancellationToken);

            var isCompletedSuccessfully = saveResults.All(sr => sr.HttpStatusCode == HttpStatusCode.OK);

            for (var index = 0; index < saveResults.Length; index++)
            {
                // get the save command and save result
                var saveCommand = _saveCommands[index];
                var saveResult = saveResults[index];

                // if successful, update the save command
                var readResult = isCompletedSuccessfully
                    ? saveCommand.Update(saveResult.Item!)
                    : null;

                // release the save command
                saveCommand.Release();

                // create the read result
                batchResults[index] = new BatchResult<TInterface, TItem>(
                    saveResult.HttpStatusCode,
                    readResult);
            }

            return batchResults;
        }
        finally
        {
            // release the exclusive lock
            _semaphore.Release();
        }
    }

    /// <summary>
    /// The action to validate the batch of items.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>The array of fluent <see cref="ValidationResult"/> item that was saved.</returns>
    public async Task<ValidationResult[]> ValidateAsync(
        CancellationToken cancellationToken = default)
    {
        // ensure that only one operation that modifies the batch is in progress at a time
        _semaphore.Wait();

        // allocate the results
        var validationResults = new ValidationResult[_saveCommands.Count];

        // iterate over the save commands and validate
        for (var index = 0; index < _saveCommands.Count; index++)
        {
            validationResults[index] = await _saveCommands[index]
                .ValidateAsync(cancellationToken);
        }

        // release the exclusive lock
        _semaphore.Release();

        return validationResults;
    }
}
