namespace Trelnex.Core.Data;

internal record BatchItem<TInterface, TItem>(
    TItem Item,
    ItemEvent<TItem> ItemEvent,
    SaveAction SaveAction)
    where TInterface : class, IBaseItem
    where TItem : BaseItem, TInterface;
