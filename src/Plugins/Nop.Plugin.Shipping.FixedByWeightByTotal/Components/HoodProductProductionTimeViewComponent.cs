using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.Shipping.FixedByWeightByTotal.Models.ProductionTime;
using Nop.Plugin.Shipping.FixedByWeightByTotal.Services.ProductionTime;

namespace Nop.Plugin.Shipping.FixedByWeightByTotal.Components;

public class HoodProductProductionTimeViewComponent : ViewComponent
{
    private readonly IProductProductionTimeService _productionTimeService;

    public HoodProductProductionTimeViewComponent(IProductProductionTimeService productionTimeService)
    {
        _productionTimeService = productionTimeService;
    }

    public async Task<IViewComponentResult> InvokeAsync(string widgetZone, object additionalData = null)
    {
        var productId = ResolveProductId(additionalData);
        if (productId <= 0)
            return Content(string.Empty);

        var model = await _productionTimeService.GetModelByProductIdAsync(productId);
        if (model == null || !model.HasRecord || !model.Enabled || !model.DisplayOnProductPage)
            return Content(string.Empty);

        return View("~/Plugins/Shipping.FixedByWeightByTotal/Views/Components/HoodProductProductionTime/Default.cshtml", model);
    }

    private int ResolveProductId(object additionalData)
    {
        if (additionalData is int intId)
            return intId;

        var id = ResolveFromObject(additionalData);
        if (id > 0)
            return id;

        if (ViewContext?.RouteData?.Values?.TryGetValue("productId", out var routeProductId) == true && int.TryParse(routeProductId?.ToString(), out id))
            return id;

        if (ViewContext?.RouteData?.Values?.TryGetValue("id", out var routeId) == true && int.TryParse(routeId?.ToString(), out id))
            return id;

        return 0;
    }

    private static int ResolveFromObject(object value)
    {
        if (value == null)
            return 0;

        var type = value.GetType();
        foreach (var name in new[] { "Id", "ProductId", "ProductID" })
        {
            var prop = type.GetProperty(name);
            if (prop == null)
                continue;

            var raw = prop.GetValue(value);
            if (raw != null && int.TryParse(raw.ToString(), out var id) && id > 0)
                return id;
        }

        return 0;
    }
}
