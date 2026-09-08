namespace Nop.Plugin.Widgets.HoodAdsSignalTracking.Models;

public sealed record ProductSignalModel(
    int ProductId,
    string ItemId,
    string ItemName,
    decimal? Price,
    string Currency,
    string Category);
