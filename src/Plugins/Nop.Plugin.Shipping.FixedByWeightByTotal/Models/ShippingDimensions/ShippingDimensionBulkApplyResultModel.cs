namespace Nop.Plugin.Shipping.FixedByWeightByTotal.Models.ShippingDimensions;

public class ShippingDimensionBulkApplyResultModel
{
    public int CategoryId { get; set; }
    public int SourceProductId { get; set; }
    public int TargetProductCount { get; set; }
    public int SourceRuleCount { get; set; }
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public int Excluded { get; set; }
    public IList<string> Messages { get; set; } = new List<string>();
}
