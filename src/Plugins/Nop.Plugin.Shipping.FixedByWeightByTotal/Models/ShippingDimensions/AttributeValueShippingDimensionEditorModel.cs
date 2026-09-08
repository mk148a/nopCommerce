namespace Nop.Plugin.Shipping.FixedByWeightByTotal.Models.ShippingDimensions;

public class AttributeValueShippingDimensionEditorModel
{
    public int ProductId { get; set; }
    public int ProductAttributeValueId { get; set; }
    public int? ExistingRuleId { get; set; }
    public bool Enabled { get; set; }
    public string RuleType { get; set; } = "ATTRIBUTE_VALUE";
    public decimal? LengthCm { get; set; }
    public decimal? WidthCm { get; set; }
    public decimal? HeightCm { get; set; }
    public decimal? WeightGram { get; set; }
    public decimal Divisor { get; set; } = 5000m;
    public int PackageCount { get; set; } = 1;
    public bool IsShipSeparately { get; set; }
    public string Source { get; set; }
}
