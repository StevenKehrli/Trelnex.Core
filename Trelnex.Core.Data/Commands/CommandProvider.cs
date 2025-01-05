using System.Text.RegularExpressions;
using FluentValidation;
using FluentValidation.Results;

namespace Trelnex.Core.Data;

/// <summary>
/// The interface to expose the commands against the backing data store.
/// </summary>
/// <typeparam name="TInterface">The interface type of the items in the backing data store.</typeparam>
public interface ICommandProvider<TInterface>
    where TInterface : class, IBaseItem
{
    /// <summary>
    /// Returns an <see cref="ISaveCommand{TInterface}"/> which wraps the item.
    /// </summary>
    /// <param name="id">The id of the item.</param>
    /// <param name="partitionKey">The partition key of the item.</param>
    /// <returns>An <see cref="ISaveCommand{TInterface}"/> with the item that was created.</returns>
    ISaveCommand<TInterface> Create(
        string id,
        string partitionKey);

    /// <summary>
    /// Reads a item from the backing data store as an asynchronous operation
    /// and returns an <see cref="ISaveCommand{TInterface}"/> which wraps the item.
    /// </summary>
    /// <param name="id">The id of the item.</param>
    /// <param name="partitionKey">The partition key of the item.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> representing request cancellation.</param>
    /// <returns>An <see cref="ISaveCommand{TInterface}"/> with the item that was read.</returns>
    Task<ISaveCommand<TInterface>?> DeleteAsync(
        string id,
        string partitionKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a item from the backing data store as an asynchronous operation.
    /// </summary>
    /// <param name="id">The id of the item.</param>
    /// <param name="partitionKey">The partition key of the item.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> representing request cancellation.</param>
    /// <returns>An <see cref="IReadResult{TInterface}"/> with the item that was read.</returns>
    Task<IReadResult<TInterface>?> ReadAsync(
        string id,
        string partitionKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a item from the backing data store as an asynchronous operation
    /// and returns an <see cref="ISaveCommand{TInterface}"/> which wraps the item.
    /// </summary>
    /// <param name="id">The id of the item.</param>
    /// <param name="partitionKey">The partition key of the item.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> representing request cancellation.</param>
    /// <returns>An <see cref="ISaveCommand{TInterface}"/> with the item that was read.</returns>
    Task<ISaveCommand<TInterface>?> UpdateAsync(
        string id,
        string partitionKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a LINQ query for items from the backing data store.
    /// </summary>
    /// <returns>The <see cref="IQueryCommand{TInterface}"/>.</returns>
    public IQueryCommand<TInterface> Query();
}

internal abstract partial class CommandProvider<TInterface, TItem>
    : ICommandProvider<TInterface>
    where TInterface : class, IBaseItem
    where TItem : BaseItem, TInterface, new()
{
    /// <summary>
    /// The type name of the item.
    /// </summary>
    protected readonly string _typeName;

    /// <summary>
    /// The fluent validator for the base item.
    /// </summary>
    private readonly AbstractValidator<TItem> _baseItemValidator;

    /// <summary>
    /// The fluent validator for the item.
    /// </summary>
    private readonly AbstractValidator<TItem>? _itemValidator;

    /// <summary>
    /// The <see cref="ExpressionConverter{TInterface,TItem}"/> to convert an expression using a TInterface to an expression using a TItem.
    /// </summary>
    private readonly ExpressionConverter<TInterface, TItem> _expressionConverter;

    /// <summary>
    /// The command operations allowed by this provider.
    /// </summary>
    private readonly CommandOperations _commandOperations;

    /// <summary>
    /// The type name of the item - used for <see cref="BaseItem.TypeName"/>.
    /// </summary>
    protected string TypeName => _typeName;

    /// <summary>
    ///
    /// </summary>
    /// <param name="typeName">The type name of the item.</param>
    /// <param name="validator">The fluent validator for the item.</param>
    /// <param name="commandOperations">The command operations allowed by this provider.</param>
    protected CommandProvider(
        string typeName,
        AbstractValidator<TItem>? validator,
        CommandOperations? commandOperations = null)
    {
        // validate the type folloows the naming rules
        if (TypeRulesRegex().IsMatch(typeName) is false)
        {
            throw new ArgumentException($"The typeName '{typeName}' does not follow the naming rules: lowercase letters and hyphens; start and end with a lowercase letter.", nameof(typeName));
        }

        // validate not a reserved type
        if (ReservedTypeNames.IsReserved(typeName))
        {
            throw new ArgumentException($"The typeName '{typeName}' is a reserved type name.", nameof(typeName));
        }

        // set the inputs
        _typeName = typeName;

        // set the validators
        _baseItemValidator = CreateBaseItemValidator(typeName);
        _itemValidator = validator;

        // create the expression converter
        _expressionConverter = new();

        // the command operations allowed by this provider
        _commandOperations = commandOperations ?? CommandOperations.Update;
    }

    /// <summary>
    /// Returns an <see cref="ISaveCommand{TInterface}"/> which wraps the item.
    /// </summary>
    /// <param name="id">The id of the item.</param>
    /// <param name="partitionKey">The partition key of the item.</param>
    /// <returns>An <see cref="ISaveCommand{TInterface}"/> with the item that was created.</returns>
    public ISaveCommand<TInterface> Create(
        string id,
        string partitionKey)
    {
        var dateTimeUtcNow = DateTime.UtcNow;

        var item = new TItem
        {
            Id = id,
            PartitionKey = partitionKey,

            TypeName = _typeName,

            CreatedDate = dateTimeUtcNow,
            UpdatedDate = dateTimeUtcNow,
        };

        return SaveCommand<TInterface, TItem>.Create(
            item: item,
            isReadOnly: false,
            validateAsyncDelegate: ValidateAsync,
            saveAction: SaveAction.CREATED,
            saveAsyncDelegate: CreateItemAsync);
    }

    /// <summary>
    /// Reads a item from the backing data store as an asynchronous operation
    /// and returns an <see cref="ISaveCommand{TInterface}"/> which wraps the item.
    /// </summary>
    /// <param name="id">The id of the item.</param>
    /// <param name="partitionKey">The partition key of the item.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> representing request cancellation.</param>
    /// <returns>An <see cref="ISaveCommand{TInterface}"/> with the item that was read.</returns>
    public async Task<ISaveCommand<TInterface>?> DeleteAsync(
        string id,
        string partitionKey,
        CancellationToken cancellationToken = default)
    {
        if (_commandOperations.HasFlag(CommandOperations.Delete) is false)
        {
            throw new NotSupportedException("The requested Delete operation is not supported.");
        }

        var item = await ReadItemAsync(id, partitionKey, cancellationToken);

        if (item is null || (item.IsDeleted ?? false))
        {
            return null;
        }

        return CreateDeleteCommand(item);
    }

    /// <summary>
    /// Reads a item from the backing data store as an asynchronous operation.
    /// </summary>
    /// <param name="id">The id of the item.</param>
    /// <param name="partitionKey">The partition key of the item.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> representing request cancellation.</param>
    /// <returns>The item that was read.</returns>
    public async Task<IReadResult<TInterface>?> ReadAsync(
        string id,
        string partitionKey,
        CancellationToken cancellationToken = default)
    {
        var item = await ReadItemAsync(id, partitionKey, cancellationToken);

        if (item is null || (item.IsDeleted ?? false))
        {
            return null;
        }

        return CreateReadResult(item);
    }

    /// <summary>
    /// Reads a item from the backing data store as an asynchronous operation
    /// and returns an <see cref="ISaveCommand{TInterface}"/> which wraps the item.
    /// </summary>
    /// <param name="id">The id of the item.</param>
    /// <param name="partitionKey">The partition key of the item.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> representing request cancellation.</param>
    /// <returns>An <see cref="ISaveCommand{TInterface}"/> with the item that was read.</returns>
    public async Task<ISaveCommand<TInterface>?> UpdateAsync(
        string id,
        string partitionKey,
        CancellationToken cancellationToken = default)
    {
        if (_commandOperations.HasFlag(CommandOperations.Update) is false)
        {
            throw new NotSupportedException("The requested Update operation is not supported.");
        }

        var item = await ReadItemAsync(id, partitionKey, cancellationToken);

        if (item is null || (item.IsDeleted ?? false))
        {
            return null;
        }

        return CreateUpdateCommand(item);
    }

    /// <summary>
    /// Creates a LINQ query for items from the backing data store.
    /// </summary>
    /// <returns>The <see cref="IQueryCommand{TInterface}"/>.</returns>
    public IQueryCommand<TInterface> Query()
    {
        // create the query command
        return CreateQueryCommand(
            expressionConverter: _expressionConverter,
            convertToQueryResult: item => {
                return QueryResult<TInterface, TItem>.Create(
                    item: item,
                    validateAsyncDelegate: ValidateAsync);
            });
    }

    /// <summary>
    /// Creates a item in the backing data store as an asynchronous operation.
    /// </summary>
    /// <param name="item">The item to create.</param>
    /// <param name="itemEvent">The <see cref="ItemEvent"> that represents information regarding the item and the caller that invoked the save method.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> representing request cancellation.</param>
    /// <returns>The item that was created.</returns>
    protected abstract Task<TItem> CreateItemAsync(
        TItem item,
        ItemEvent<TItem> itemEvent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a item from the backing data store as an asynchronous operation.
    /// </summary>
    /// <param name="id">The id of the item.</param>
    /// <param name="partitionKey">The partition key of the item.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> representing request cancellation.</param>
    /// <returns>The item that was read.</returns>
    protected abstract Task<TItem?> ReadItemAsync(
        string id,
        string partitionKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a item in the backing data store as an asynchronous operation.
    /// </summary>
    /// <param name="item">The item to update.</param>
    /// <param name="itemEvent">The <see cref="ItemEvent"> that represents information regarding the item and the caller that invoked the save method.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> representing request cancellation.</param>
    /// <returns>The item that was updated.</returns>
    protected abstract Task<TItem> UpdateItemAsync(
        TItem item,
        ItemEvent<TItem> itemEvent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create an instance of the <see cref="IQueryCommand{Interface}"/>.
    /// </summary>
    /// <param name="expressionConverter">The <see cref="ExpressionConverter{TInterface,TItem}"/> to convert an expression using a TInterface to an expression using a TItem.</param>
    /// <param name="convertToQueryResult">The method to convert a TItem to a <see cref="IQueryResult{TInterface}"/>.</param>
    /// <returns>The <see cref="IQueryCommand{Interface}"/>.</returns>
    protected abstract IQueryCommand<TInterface> CreateQueryCommand(
        ExpressionConverter<TInterface, TItem> expressionConverter,
        Func<TItem, IQueryResult<TInterface>> convertToQueryResult);

    /// <summary>
    /// Create an <see cref="ISaveCommand{TInterface}"/> to delete the item.
    /// </summary>
    /// <returns>An <see cref="ISaveCommand{TInterface}"/> with the item to delete.</returns>
    private ISaveCommand<TInterface> CreateDeleteCommand(
        TItem item)
    {
        item.DeletedDate = DateTime.UtcNow;
        item.IsDeleted = true;

        return SaveCommand<TInterface, TItem>.Create(
            item: item,
            isReadOnly: true,
            validateAsyncDelegate: ValidateAsync,
            saveAction: SaveAction.DELETED,
            saveAsyncDelegate: UpdateItemAsync);        
    }

    /// <summary>
    /// Create a <see cref="IReadResult{TInterface}"/> over the item.
    /// </summary>
    /// <param name="item">The item to create a proxy.</param>
    /// <returns>The <see cref="IReadResult{TInterface}"/>.</returns>
    private IReadResult<TInterface> CreateReadResult(
        TItem item)
    {
        return ReadResult<TInterface, TItem>.Create(
            item: item,
            validateAsyncDelegate: ValidateAsync);
    }

    /// <summary>
    /// Create an <see cref="ISaveCommand{TInterface}"/> to update the item.
    /// </summary>
    /// <returns>An <see cref="ISaveCommand{TInterface}"/> with the item to delete.</returns>
    private ISaveCommand<TInterface> CreateUpdateCommand(
        TItem item)
    {
        item.UpdatedDate = DateTime.UtcNow;

        return SaveCommand<TInterface, TItem>.Create(
            item: item,
            isReadOnly: false,
            validateAsyncDelegate: ValidateAsync,
            saveAction: SaveAction.UPDATED,
            saveAsyncDelegate: UpdateItemAsync);
    }

    /// <summary>
    /// Validate the item using the base item validator and the item validation.
    /// </summary>
    /// <param name="item">The item to validate.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>The fluent <see cref="ValidationResult"/>.</returns>
    private async Task<ValidationResult> ValidateAsync(
        TItem item,
        CancellationToken cancellationToken)
    {
        // create a composite of the base item validator and the item validator
        var compositeValidator = new CompositeValidator<TItem>(_baseItemValidator, _itemValidator);

        // validate the item
        return await compositeValidator.ValidateAsync(item, cancellationToken);
    }

    [GeneratedRegex("^[a-z]+[a-z-]*[a-z]+$")]
    private static partial Regex TypeRulesRegex();

    /// <summary>
    /// Create an instance of <see cref="AbstractValidator{TItem}"/> to validate the base item.
    /// </summary>
    /// <param name="typeName">The type name of the item - used for <see cref="BaseItem.TypeName"/>.</param>
    /// <returns>The <see cref="AbstractValidator{TItem}"/>.</returns>
    private static AbstractValidator<TItem> CreateBaseItemValidator(
        string typeName)
    {
        AbstractValidator<TItem> baseItemValidator = new InlineValidator<TItem>();

        // id
        baseItemValidator.RuleFor(k => k.Id)
            .NotEmpty()
            .WithMessage("Id is null or empty.");

        // partitionKey
        baseItemValidator.RuleFor(k => k.PartitionKey)
            .NotEmpty()
            .WithMessage("PartitionKey is null or empty.");

        // type
        baseItemValidator.RuleFor(k => k.TypeName)
            .Must(k => k == typeName)
            .WithMessage($"TypeName is not '{typeName}'.");

        // createdDate
        baseItemValidator.RuleFor(k => k.CreatedDate)
            .NotDefault()
            .WithMessage($"CreatedDate is not valid.");

        // updatedDate
        baseItemValidator.RuleFor(k => k.UpdatedDate)
            .NotDefault()
            .WithMessage($"UpdatedDate is not valid.");

        return baseItemValidator;
    }

    /// <summary>
    /// The delegate to save the item.
    /// </summary>
    /// <param name="item">The item to save.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    private delegate Task<TItem> SaveActionDelegate(
        TItem item,
        CancellationToken cancellationToken);
}
