using Nop.Core;

namespace Nop.Plugin.Shipping.FixedByWeightByTotal.Domain;

/// <summary>
/// Stores package dimensions and shipping weight overrides for product attribute values or full attribute combinations.
/// This is used before the existing FixedByWeightByTotal rate lookup so rates are calculated by chargeable weight.
/// </summary>
public class HoodProductShippingDimensionRule : BaseEntity
{
    public int ProductId { get; set; }

    /// <summary>
    /// Optional selected ProductAttributeValueId. Good for simple rules such as Pcs=6/12/24.
    /// </summary>
    public int? ProductAttributeValueId { get; set; }

    /// <summary>
    /// Sorted CSV of selected ProductAttributeValueId values. Example: 423,430,440.
    /// </summary>
    public string AttributeValueIdsCsv { get; set; }

    /// <summary>
    /// SHA256 hash of AttributeValueIdsCsv for exact-combination lookup.
    /// </summary>
    public string AttributeHash { get; set; }

    /// <summary>
    /// PRODUCT_DEFAULT, ATTRIBUTE_VALUE, ATTRIBUTE_COMBINATION, ARROW_PCS.
    /// </summary>
    public string RuleType { get; set; }

    public decimal LengthCm { get; set; }
    public decimal WidthCm { get; set; }
    public decimal HeightCm { get; set; }

    /// <summary>
    /// Actual package weight in grams. If null, native nop product + attribute weight is used.
    /// </summary>
    public decimal? WeightGram { get; set; }

    /// <summary>
    /// Standard divisor is 5000 for cm to kg volumetric calculation.
    /// </summary>
    public decimal Divisor { get; set; } = 5000m;

    public int PackageCount { get; set; } = 1;
    public bool IsShipSeparately { get; set; }
    public bool IsActive { get; set; } = true;

    public int SampleCount { get; set; }
    public string Confidence { get; set; }
    public string Source { get; set; }
    public string ExampleShipmentNo { get; set; }
    public int? ExampleOrderId { get; set; }

    public DateTime CreatedOnUtc { get; set; }
    public DateTime? UpdatedOnUtc { get; set; }
}
