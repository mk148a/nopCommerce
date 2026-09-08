using Nop.Core;

namespace Nop.Plugin.Shipping.FixedByWeightByTotal.Domain;

/// <summary>
/// Products excluded from automatic Navlungo learning and category bulk-apply operations.
/// Runtime manually-created dimension rules can still be used for these products.
/// </summary>
public class HoodProductShippingDimensionExclusion : BaseEntity
{
    public int ProductId { get; set; }
    public string Reason { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? UpdatedOnUtc { get; set; }
}
