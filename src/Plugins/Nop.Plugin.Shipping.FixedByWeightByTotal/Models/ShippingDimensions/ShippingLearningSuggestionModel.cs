namespace Nop.Plugin.Shipping.FixedByWeightByTotal.Models.ShippingDimensions;

public class ShippingLearningSuggestionModel
{
    public int ProductId { get; set; }
    public string ProductName { get; set; }
    public int? ProductAttributeValueId { get; set; }
    public string ProductAttributeValueName { get; set; }
    public string AttributeHash { get; set; }
    public string RuleType { get; set; }

    public int SampleCount { get; set; }
    public decimal SuggestedLengthCm { get; set; }
    public decimal SuggestedWidthCm { get; set; }
    public decimal SuggestedHeightCm { get; set; }
    public decimal SuggestedWeightGram { get; set; }
    public decimal SuggestedChargeableWeightKg { get; set; }

    public string Confidence { get; set; }
    public string ExampleShipmentNo { get; set; }
    public int? ExampleOrderId { get; set; }
    public string ExampleAttributeDescription { get; set; }
}
