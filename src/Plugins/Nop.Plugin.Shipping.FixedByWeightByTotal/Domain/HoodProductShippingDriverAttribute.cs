using Nop.Core;

namespace Nop.Plugin.Shipping.FixedByWeightByTotal.Domain;

/// <summary>
/// Marks which product attribute mapping controls shipping dimensions for learning and runtime matching.
/// Example: arrows -> Pcs, armor -> Armor Sets.
/// </summary>
public class HoodProductShippingDriverAttribute : BaseEntity
{
    public int ProductId { get; set; }
    public int ProductAttributeMappingId { get; set; }
    public int ProductAttributeId { get; set; }
    public string ProductAttributeName { get; set; }
    public string RuleType { get; set; } = "ATTRIBUTE_VALUE";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? UpdatedOnUtc { get; set; }
}
