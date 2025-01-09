namespace Trelnex.Core.Data;

internal record SaveContext<TInterface, TItem>(
    TItem Item,
    ItemEvent<TItem> Event,
    SaveAction SaveAction)
    where TInterface : class, IBaseItem
    where TItem : BaseItem, TInterface;
