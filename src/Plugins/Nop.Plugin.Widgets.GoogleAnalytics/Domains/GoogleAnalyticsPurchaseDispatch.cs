using Nop.Core;

namespace Nop.Plugin.Widgets.GoogleAnalytics.Domains;

/// <summary>
/// Records the one browser-side GA4 purchase dispatch allowed for an order.
/// This is deliberately separate from the order itself so a page refresh,
/// a second browser tab, or a concurrent request cannot create another
/// conversion event.
/// </summary>
public class GoogleAnalyticsPurchaseDispatch : BaseEntity
{
    public int OrderId { get; set; }

    public DateTime CreatedOnUtc { get; set; }
}
