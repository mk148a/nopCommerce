using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.Widgets.HoodAdsSignalTracking.Models;
using Nop.Web.Framework.Components;
using Nop.Web.Framework.Infrastructure;
using Nop.Web.Models.Catalog;

namespace Nop.Plugin.Widgets.HoodAdsSignalTracking.Components;

[ViewComponent(Name = "HoodAdsSignalTracking")]
public sealed class HoodAdsSignalTrackingViewComponent : NopViewComponent
{
    public Task<IViewComponentResult> InvokeAsync(string widgetZone, object additionalData)
    {
        if (string.Equals(widgetZone, PublicWidgetZones.BodyEndHtmlTagBefore, StringComparison.Ordinal))
            return Task.FromResult<IViewComponentResult>(View("~/Plugins/Widgets.HoodAdsSignalTracking/Views/Components/HoodAdsSignalTracking/SiteSignals.cshtml"));

        if (!string.Equals(widgetZone, PublicWidgetZones.ProductDetailsBeforeCollateral, StringComparison.Ordinal)
            || additionalData is not ProductDetailsModel product || product.NoIndex || product.Id <= 0)
            return Task.FromResult<IViewComponentResult>(Content(string.Empty));

        var category = product.Breadcrumb?.CategoryBreadcrumb?.LastOrDefault()?.Name;
        var price = product.ProductPrice.PriceWithDiscountValue ?? product.ProductPrice.PriceValue;
        var itemId = string.IsNullOrWhiteSpace(product.Sku) ? product.Id.ToString() : product.Sku;

        return Task.FromResult<IViewComponentResult>(View("~/Plugins/Widgets.HoodAdsSignalTracking/Views/Components/HoodAdsSignalTracking/ProductSignal.cshtml",
            new ProductSignalModel(product.Id, itemId, product.Name, price, product.ProductPrice.CurrencyCode, category)));
    }
}
