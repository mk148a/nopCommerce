using Nop.Core.Configuration;

namespace Nop.Plugin.Shipping.FixedByWeightByTotal;

/// <summary>
/// Represents settings of the "Fixed or by weight" shipping plugin
/// </summary>
public class FixedByWeightByTotalSettings : ISettings
{
    /// <summary>
    /// Gets or sets a value indicating whether to limit shipping methods to configured ones
    /// </summary>
    public bool LimitMethodsToCreated { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the "shipping calculation by weight and by total" method is selected
    /// </summary>
    public bool ShippingByWeightByTotalEnabled { get; set; }
        
    /// <summary>
    /// Gets or sets a value indicating whether to load all shipping by weight records in one request
    /// </summary>
    public bool LoadAllRecord { get; set; }

    /// <summary>
    /// Enables Hood/Navlungo chargeable weight calculation before the existing rate table lookup.
    /// </summary>
    public bool HoodNavlungoChargeableWeightEnabled { get; set; } = true;

    /// <summary>
    /// cm³ divisor for dimensional weight. 5000 means L*W*H/5000 = kg.
    /// </summary>
    public decimal HoodNavlungoDimensionalWeightDivisor { get; set; } = 5000m;

    /// <summary>
    /// Multiplier from calculated kg to the existing FixedByWeightByTotal rate-table weight unit.
    /// Your store currently uses gram-based product weights, so default is 1000.
    /// Use 1 if rate tables are kg-based.
    /// </summary>
    public decimal HoodNavlungoRateWeightMultiplier { get; set; } = 1000m;
}
