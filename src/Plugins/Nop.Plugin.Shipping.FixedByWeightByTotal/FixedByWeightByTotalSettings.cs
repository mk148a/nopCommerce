using Nop.Core.Configuration;

namespace Nop.Plugin.Shipping.FixedByWeightByTotal;

/// <summary>
/// Represents settings of the "Fixed or by weight" shipping plugin
/// </summary>
public class FixedByWeightByTotalSettings : ISettings
{
    /// <summary>
    /// Comma-separated SKUs for non-merchandise payment or adjustment products. These
    /// products remain purchasable but are excluded from Product structured data.
    /// </summary>
    public string SystemProductSkus { get; set; } = "expresshipping";

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

    /// <summary>
    /// Enables the live PTT international parcel option. This option is added in addition to the normal rate-table methods.
    /// </summary>
    public bool HoodPttPostServiceEnabled { get; set; } = false;

    /// <summary>
    /// Comma separated product IDs that are allowed to show the PTT Post service option.
    /// If both product and category lists are empty, the PTT option is hidden.
    /// </summary>
    public string HoodPttEligibleProductIdsCsv { get; set; }

    /// <summary>
    /// Comma separated category IDs. Products mapped to these categories are allowed to show the PTT Post service option.
    /// </summary>
    public string HoodPttEligibleCategoryIdsCsv { get; set; }

    /// <summary>
    /// Public method name shown to customers for the live PTT option.
    /// </summary>
    public string HoodPttMethodName { get; set; } = "Post service";

    /// <summary>
    /// PTT DeliveryKind parameter. For international parcel/koli, current PTT calculator uses YD KOLİ.
    /// </summary>
    public string HoodPttDeliveryKind { get; set; } = "YD KOLİ";

    /// <summary>
    /// PTT DistributionType parameter observed from the live calculator.
    /// </summary>
    public string HoodPttDistributionType { get; set; } = "UC";

    /// <summary>
    /// PTT additionalService parameter observed from the live calculator. Empty means send an empty value.
    /// </summary>
    public string HoodPttAdditionalService { get; set; } = "GM";

    /// <summary>
    /// Maximum allowed package single side, in cm, for showing PTT Post service.
    /// </summary>
    public decimal HoodPttMaxSingleDimensionCm { get; set; } = 150m;

    /// <summary>
    /// Maximum allowed PTT girth formula: longest side + 2*other side + 2*other side. 0 disables this check.
    /// </summary>
    public decimal HoodPttMaxGirthCm { get; set; } = 300m;

    /// <summary>
    /// PTT option transit estimate minimum days. Production time is added on top of this.
    /// </summary>
    public int HoodPttTransitMinDays { get; set; } = 10;

    /// <summary>
    /// PTT option transit estimate maximum days. Production time is added on top of this.
    /// </summary>
    public int HoodPttTransitMaxDays { get; set; } = 20;

    /// <summary>
    /// Multiplier to convert the live PTT response amount into the store primary currency.
    /// Example: if PTT returns TRY and store currency is USD, set TRY->USD multiplier here.
    /// </summary>
    public decimal HoodPttLivePriceMultiplier { get; set; } = 1m;

    /// <summary>
    /// Optional fixed markup in store currency, added after live PTT price conversion.
    /// </summary>
    public decimal HoodPttAdditionalFixedMarkup { get; set; } = 0m;

    /// <summary>
    /// Live PTT API timeout in seconds.
    /// </summary>
    public int HoodPttRequestTimeoutSeconds { get; set; } = 8;
}
