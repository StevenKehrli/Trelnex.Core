namespace Trelnex.Core.Data;

internal delegate Task<TItem> SaveAsyncDelegate<TInterface, TItem>(
    SaveContext<TInterface, TItem> saveContext,
    CancellationToken cancellationToken)
    where TInterface : class, IBaseItem
    where TItem : BaseItem, TInterface;
