namespace Nop.Plugin.Shipping.FixedByWeightByTotal.Models.ProductionTime;

public class ProductProductionTimeModel
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; }
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
    public bool HasRecord { get; set; }
    public bool IsOrderable { get; set; }

    public bool HideNumericStock => IsHandmade && IsOrderable;

    public string AvailabilityText => HideNumericStock ? "Available to order — made to order" : null;

    public string EffectiveProductionText
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(ProductionTimeText))
                return ProductionTimeText;

            if (ProductionMinDays <= 0 && ProductionMaxDays <= 0)
                return string.Empty;

            if (ProductionMinDays > 0 && ProductionMaxDays > 0 && ProductionMinDays != ProductionMaxDays)
                return $"{ProductionMinDays}-{ProductionMaxDays} days";

            var value = Math.Max(ProductionMinDays, ProductionMaxDays);
            return value <= 0 ? string.Empty : $"{value} days";
        }
    }
}
