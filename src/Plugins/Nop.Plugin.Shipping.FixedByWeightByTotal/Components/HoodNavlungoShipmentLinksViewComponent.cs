using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.Shipping.FixedByWeightByTotal.Services.ShippingDimensions;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Shipping.FixedByWeightByTotal.Components;

public class HoodNavlungoShipmentLinksViewComponent : NopViewComponent
{
    private readonly IProductShippingDimensionService _productShippingDimensionService;

    public HoodNavlungoShipmentLinksViewComponent(IProductShippingDimensionService productShippingDimensionService)
    {
        _productShippingDimensionService = productShippingDimensionService;
    }

    public async Task<IViewComponentResult> InvokeAsync(string widgetZone, object additionalData)
    {
        var shipmentId = TryGetInt(additionalData, "Id");
        var orderId = TryGetInt(additionalData, "OrderId");
        var trackingNumber = TryGetString(additionalData, "TrackingNumber");

        var links = await _productShippingDimensionService.GetShipmentLinksAsync(shipmentId, orderId, trackingNumber);

        ViewBag.ShipmentId = shipmentId;
        ViewBag.OrderId = orderId;
        ViewBag.TrackingNumber = trackingNumber;

        return View("~/Plugins/Shipping.FixedByWeightByTotal/Views/Components/HoodNavlungoShipmentLinks/Default.cshtml", links);
    }

    private static int? TryGetInt(object source, string propertyName)
    {
        if (source == null)
            return null;

        var prop = source.GetType().GetProperty(propertyName);
        if (prop == null)
            return null;

        var value = prop.GetValue(source);
        if (value == null)
            return null;

        if (int.TryParse(value.ToString(), out var i) && i > 0)
            return i;

        return null;
    }

    private static string TryGetString(object source, string propertyName)
    {
        if (source == null)
            return null;

        var prop = source.GetType().GetProperty(propertyName);
        return prop?.GetValue(source)?.ToString();
    }
}
