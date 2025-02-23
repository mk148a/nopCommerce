using Nop.Core.Configuration;

namespace Nop.Plugin.Misc.GoogleBotAggregator;

/// <summary>
/// Google Bot Aggregator ayarları
/// </summary>
public class GoogleBotAggregatorSettings : ISettings
{
    /// <summary>
    /// Gets or sets a value indicating whether to exclude from analytics
    /// </summary>
    public bool ExcludeFromAnalytics { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to merge shopping carts
    /// </summary>
    public bool MergeShoppingCarts { get; set; } = true;

    /// <summary>
    /// Gets or sets the Google Bot customer email
    /// </summary>
    public string GoogleBotCustomerEmail { get; set; } = "googlebot@example.com";
} 