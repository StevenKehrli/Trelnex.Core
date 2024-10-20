namespace Trelnex.Core.Data;

internal delegate Task<TItem> SaveAsyncDelegate<TInterface, TItem>(
    TItem item,
    ItemEvent<TItem> itemEvent,
    CancellationToken cancellationToken)
    where TInterface : class, IBaseItem
    where TItem : BaseItem, TInterface;
