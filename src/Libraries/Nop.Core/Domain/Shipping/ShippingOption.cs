namespace Nop.Core.Domain.Shipping;

/// <summary>
/// Represents a shipping option
/// </summary>
public partial class ShippingOption
{
    /// <summary>
    /// Gets or sets the system name of shipping rate computation method
    /// </summary>
    public string ShippingRateComputationMethodSystemName { get; set; }

    /// <summary>
    /// Gets or sets a shipping rate (without discounts, additional shipping charges, etc)
    /// </summary>
    public decimal Rate { get; set; }

    /// <summary>
    /// Gets or sets a shipping option name
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets a shipping option description
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets a transit days
    /// </summary>
    public int? TransitDays { get; set; }

    /// <summary>
    /// Gets or sets the carrier transit minimum when the provider exposes a range.
    /// This is optional metadata; TransitDays remains the legacy checkout-compatible value.
    /// </summary>
    public int? TransitMinDays { get; set; }

    /// <summary>
    /// Gets or sets the carrier transit maximum when the provider exposes a range.
    /// </summary>
    public int? TransitMaxDays { get; set; }

    /// <summary>
    /// Gets or sets the production/handling minimum used for this quote.
    /// </summary>
    public int? HandlingMinDays { get; set; }

    /// <summary>
    /// Gets or sets the production/handling maximum used for this quote.
    /// </summary>
    public int? HandlingMaxDays { get; set; }

    /// <summary>
    /// Gets or sets the ISO 3166-1 alpha-2 destination used for this quote.
    /// </summary>
    public string DestinationCountryCode { get; set; }

    /// <summary>
    /// Gets or sets a value indicating if it's pickup in store shipping option
    /// </summary>
    public bool IsPickupInStore { get; set; }

    /// <summary>
    /// Gets or sets a display order
    /// </summary>
    public int? DisplayOrder { get; set; }
}
