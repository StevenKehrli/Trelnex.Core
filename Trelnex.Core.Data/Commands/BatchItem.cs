namespace Trelnex.Core.Data;

internal record BatchItem<TInterface, TItem>(
    TItem Item,
    ItemEvent<TItem> ItemEvent)
    where TInterface : class, IBaseItem
    where TItem : BaseItem, TInterface;
