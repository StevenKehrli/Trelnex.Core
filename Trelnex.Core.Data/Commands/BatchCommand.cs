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
internal class BatchCommand<TInterface, TItem>
    : IBatchCommand<TInterface>
    where TInterface : class, IBaseItem
    where TItem : BaseItem, TInterface
{
    /// <summary>
    /// An exclusive lock to ensure that only one operation that modifies the batch is in progress at a time
    /// </summary>
    protected readonly SemaphoreSlim _semaphore = new(1, 1);

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

        // allocate the results
        var readResults = new IReadResult<TInterface>[_saveCommands.Count];

        // iterate over the save commands
        for (var index = 0; index < _saveCommands.Count; index++)
        {
            var saveCommand = _saveCommands[index];

            // start the batch operation
            var batchItem = await saveCommand.StartBatchAsync(requestContext, cancellationToken);

            // finalize the batch operation
            readResults[index] = saveCommand.FinalizeBatch(batchItem.Item);
        }

        // clear the batch
        _saveCommands.Clear();

        // release the exclusive lock
        _semaphore.Release();

        return readResults;
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
