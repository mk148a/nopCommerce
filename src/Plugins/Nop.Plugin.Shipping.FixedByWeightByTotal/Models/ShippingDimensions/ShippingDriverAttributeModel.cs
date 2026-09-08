namespace Nop.Plugin.Shipping.FixedByWeightByTotal.Models.ShippingDimensions;

public class ShippingDriverAttributeModel
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int ProductAttributeMappingId { get; set; }
    public int ProductAttributeId { get; set; }
    public string ProductAttributeName { get; set; }
    public string ControlTypeName { get; set; }
    public string RuleType { get; set; }
    public bool IsActive { get; set; }
    public bool IsConfigured { get; set; }
}
