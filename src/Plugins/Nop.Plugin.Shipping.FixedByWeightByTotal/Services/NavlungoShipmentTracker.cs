using Nop.Core.Domain.Shipping;
using Nop.Services.Shipping.Tracking;

namespace Nop.Plugin.Shipping.FixedByWeightByTotal.Services;

/// <summary>
/// Lightweight tracker for Navlungo-created shipping methods.
///
/// This does not scrape carrier pages. It provides a safe external tracking URL
/// that nopCommerce can show on shipment/customer order pages. For live in-site
/// event history, use an official carrier API or a unified tracking API and
/// implement GetShipmentEventsAsync accordingly.
/// </summary>
public class NavlungoShipmentTracker : IShipmentTracker
{
    public Task<string> GetUrlAsync(string trackingNumber, Shipment shipment = null)
    {
        if (string.IsNullOrWhiteSpace(trackingNumber))
            return Task.FromResult<string>(null);

        var encodedTrackingNumber = Uri.EscapeDataString(trackingNumber.Trim());

        // 17TRACK is used only as an external redirect page.
        // It supports multiple carriers and avoids fragile HTML scraping inside the store.
        return Task.FromResult($"https://t.17track.net/en#nums={encodedTrackingNumber}");
    }

    public Task<IList<ShipmentStatusEvent>> GetShipmentEventsAsync(string trackingNumber, Shipment shipment = null)
    {
        // Do not fabricate tracking history. Return an empty list unless a real API
        // integration is added. nopCommerce will still have the external tracking URL.
        return Task.FromResult<IList<ShipmentStatusEvent>>(new List<ShipmentStatusEvent>());
    }
}
