namespace Nop.Plugin.Shipping.FixedByWeightByTotal.Services.ProductionTime;

/// <summary>
/// Combines typed production and carrier transit ranges without inventing either value.
/// </summary>
public static class DeliveryEstimateRange
{
    public static (int MinDays, int MaxDays) Combine(int productionMinDays, int productionMaxDays,
        int transitMinDays, int transitMaxDays)
    {
        productionMinDays = Math.Max(0, productionMinDays);
        productionMaxDays = Math.Max(productionMinDays, productionMaxDays);
        transitMinDays = Math.Max(0, transitMinDays);
        transitMaxDays = Math.Max(transitMinDays, transitMaxDays);

        return (productionMinDays + transitMinDays, productionMaxDays + transitMaxDays);
    }
}
