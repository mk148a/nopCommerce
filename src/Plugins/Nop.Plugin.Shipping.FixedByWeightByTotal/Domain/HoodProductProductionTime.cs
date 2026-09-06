using Nop.Core;

namespace Nop.Plugin.Shipping.FixedByWeightByTotal.Domain;

/// <summary>
/// Stores handmade / make-to-order production lead time per product.
/// Transit time is handled by shipping methods; this table stores the production part that must be added before delivery.
/// </summary>
public class HoodProductProductionTime : BaseEntity
{
    public int ProductId { get; set; }
    public bool Enabled { get; set; } = true;
    public bool IsHandmade { get; set; } = true;
    public int ProductionMinDays { get; set; }
    public int ProductionMaxDays { get; set; }
    public string ProductionTimeText { get; set; }
    public string Message { get; set; }
    public bool DisplayOnProductPage { get; set; } = true;
    public bool IncludeInShippingEstimate { get; set; } = true;
    public bool ParsedFromDescription { get; set; }
    public string Source { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? UpdatedOnUtc { get; set; }
}
