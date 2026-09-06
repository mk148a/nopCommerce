using Nop.Core;

namespace Nop.Plugin.Widgets.GoogleAnalytics.Domains;

/// <summary>
/// Durable ownership record for the sole browser-side purchase dispatch of an order.
/// </summary>
public class GoogleAnalyticsPurchaseDispatch : BaseEntity
{
    public int OrderId { get; set; }

    public string Status { get; set; }

    public string LeaseToken { get; set; }

    public DateTime? LeaseExpiresOnUtc { get; set; }

    public DateTime? ConfirmedOnUtc { get; set; }

    public DateTime CreatedOnUtc { get; set; }
}
