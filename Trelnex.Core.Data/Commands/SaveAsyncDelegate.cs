namespace Trelnex.Core.Data;

internal delegate Task<TItem> SaveAsyncDelegate<TInterface, TItem>(
    TItem item,
    ItemEvent<TItem> itemEvent,
    CancellationToken cancellationToken)
    where TInterface : class, IBaseItem
    where TItem : BaseItem, TInterface;

internal delegate Task<TItem[]> SaveBatchAsyncDelegate<TInterface, TItem>(
    string partitionKey,
    BatchItem<TInterface, TItem>[] batchItems,
    CancellationToken cancellationToken = default)
    where TInterface : class, IBaseItem
    where TItem : BaseItem, TInterface;
