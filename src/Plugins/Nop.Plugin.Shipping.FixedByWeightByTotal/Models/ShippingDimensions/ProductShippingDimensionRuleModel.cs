using Nop.Web.Framework.Models;

namespace Nop.Plugin.Shipping.FixedByWeightByTotal.Models.ShippingDimensions;

public record ProductShippingDimensionRuleModel : BaseNopEntityModel
{
    public int ProductId { get; set; }
    public string ProductName { get; set; }
    public int? ProductAttributeValueId { get; set; }
    public string ProductAttributeValueName { get; set; }
    public string AttributeValueIdsCsv { get; set; }
    public string AttributeHash { get; set; }
    public string RuleType { get; set; }

    public decimal LengthCm { get; set; }
    public decimal WidthCm { get; set; }
    public decimal HeightCm { get; set; }
    public decimal? WeightGram { get; set; }
    public decimal Divisor { get; set; } = 5000m;
    public int PackageCount { get; set; } = 1;
    public bool IsShipSeparately { get; set; }
    public bool IsActive { get; set; } = true;

    public int SampleCount { get; set; }
    public string Confidence { get; set; }
    public string Source { get; set; }
    public string ExampleShipmentNo { get; set; }
    public int? ExampleOrderId { get; set; }

    public decimal ActualWeightKg { get; set; }
    public decimal DimensionalWeightKg { get; set; }
    public decimal ChargeableWeightKg { get; set; }
}
