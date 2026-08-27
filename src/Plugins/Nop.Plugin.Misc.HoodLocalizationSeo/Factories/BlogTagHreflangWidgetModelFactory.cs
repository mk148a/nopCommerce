using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Factories;
using Nop.Web.Framework.Infrastructure;
using Nop.Web.Framework.Models.Cms;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Factories;

/// <summary>
/// Suppresses the generic Google language widget only on BlogByTag requests.
/// The widget can only copy the current tag segment into every language; the
/// Hood plugin emits semantically equivalent localized tag URLs instead.
/// </summary>
public sealed class BlogTagHreflangWidgetModelFactory : IWidgetModelFactory
{
    private const string GoogleLanguageComponent =
        "Nop.Plugin.Misc.GoogleMultiLanguageAndCurrency.Components.GoogleMultiLanguageAndCurrencyWidget";
    private const string GoogleLanguageAssembly = "Nop.Plugin.Misc.GoogleMultiLanguageAndCurrency";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IWidgetModelFactory _inner;

    public BlogTagHreflangWidgetModelFactory(IWidgetModelFactory inner,
        IHttpContextAccessor httpContextAccessor)
    {
        _inner = inner;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<List<RenderWidgetModel>> PrepareRenderWidgetModelAsync(
        string widgetZone,
        object additionalData = null,
        bool useCache = true)
    {
        var models = await _inner.PrepareRenderWidgetModelAsync(widgetZone, additionalData, useCache);
        if (!widgetZone.Equals(PublicWidgetZones.HeadHtmlTag, StringComparison.OrdinalIgnoreCase) ||
            !IsSuppressedRequest(_httpContextAccessor.HttpContext))
            return models;

        return models.Where(model => !IsGenericGoogleLanguageWidget(model.WidgetViewComponent)).ToList();
    }

    private static bool IsSuppressedRequest(HttpContext context) =>
        IsBlogByTagRequest(context) || IsUtilityRequest(context);

    private static bool IsUtilityRequest(HttpContext context)
    {
        var controller = context?.GetRouteValue(NopRoutingDefaults.RouteValue.Controller)?.ToString();
        var action = context?.GetRouteValue(NopRoutingDefaults.RouteValue.Action)?.ToString();
        if (controller?.Equals("Catalog", StringComparison.OrdinalIgnoreCase) == true &&
            (action?.Equals("ProductTagsAll", StringComparison.OrdinalIgnoreCase) == true ||
             action?.Equals("ManufacturerAll", StringComparison.OrdinalIgnoreCase) == true ||
             action?.Equals("NewProducts", StringComparison.OrdinalIgnoreCase) == true &&
             context.Request.QueryString.HasValue))
            return true;

        var segments = (context?.Request.Path.Value ?? string.Empty).Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var offset = segments.Length > 0 && IsAsciiCulture(segments[0]) ? 1 : 0;
        return segments.Length == offset + 1 &&
               (segments[offset].Equals("filterSearch", StringComparison.OrdinalIgnoreCase) ||
                segments[offset].Equals("recentlyviewedproducts", StringComparison.OrdinalIgnoreCase) ||
                segments[offset].Equals("compareproducts", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsAsciiCulture(string value) =>
        value.Length == 2 && value.All(character => character is >= 'A' and <= 'Z' or >= 'a' and <= 'z');

    private static bool IsBlogByTagRequest(HttpContext context)
    {
        var controller = context?.GetRouteValue(NopRoutingDefaults.RouteValue.Controller)?.ToString();
        var action = context?.GetRouteValue(NopRoutingDefaults.RouteValue.Action)?.ToString();
        return controller?.Equals("Blog", StringComparison.OrdinalIgnoreCase) == true &&
               action?.Equals("BlogByTag", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsGenericGoogleLanguageWidget(Type componentType)
    {
        return componentType?.FullName?.Equals(GoogleLanguageComponent, StringComparison.Ordinal) == true &&
               componentType.Assembly.GetName().Name?.Equals(GoogleLanguageAssembly,
                   StringComparison.Ordinal) == true;
    }
}
