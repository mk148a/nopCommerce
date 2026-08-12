using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nop.Core;
using Nop.Core.Domain.Seo;
using Nop.Services.Events;
using Nop.Web.Framework.Events;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Consumers;

public sealed class PageRenderingEventConsumer : IConsumer<PageRenderingEvent>
{
    private static readonly HashSet<string> CanonicalRoutes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Homepage", "Blog", "BlogPost", "Sitemap"
    };

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly SeoSettings _seoSettings;
    private readonly IWorkContext _workContext;

    public PageRenderingEventConsumer(IHttpContextAccessor httpContextAccessor,
        SeoSettings seoSettings,
        IWorkContext workContext)
    {
        _httpContextAccessor = httpContextAccessor;
        _seoSettings = seoSettings;
        _workContext = workContext;
    }

    public async Task HandleEventAsync(PageRenderingEvent eventMessage)
    {
        var context = _httpContextAccessor.HttpContext;
        if (context is null || context.GetRouteValue("area") is not null)
            return;

        if (context.GetRouteValue(NopRoutingDefaults.RouteValue.Language) is not null)
        {
            var workingLanguage = await _workContext.GetWorkingLanguageAsync();
            var request = context.Request;
            var href = $"{request.Scheme}://{request.Host}{request.PathBase}{request.Path}{request.QueryString}";
            var encodedCulture = HtmlEncoder.Default.Encode(workingLanguage.LanguageCulture);
            var encodedHref = HtmlEncoder.Default.Encode(href);
            eventMessage.Helper.AddHeadCustomParts(
                $"<link rel=\"alternate\" hreflang=\"{encodedCulture}\" href=\"{encodedHref}\" />");
        }

        var routeName = eventMessage.GetRouteName(handleDefaultRoutes: true) ?? string.Empty;
        var controller = context.GetRouteValue(NopRoutingDefaults.RouteValue.Controller)?.ToString();
        var action = context.GetRouteValue(NopRoutingDefaults.RouteValue.Action)?.ToString();

        if (_seoSettings.CanonicalUrlsEnabled &&
            (CanonicalRoutes.Contains(routeName) || IsCanonicalPublicAction(controller, action)))
        {
            var request = context.Request;
            var canonical = $"{request.Scheme}://{request.Host}{request.PathBase}{request.Path}".ToLowerInvariant();
            eventMessage.Helper.AddCanonicalUrlParts(canonical,
                _seoSettings.QueryStringInCanonicalUrlsEnabled);
        }

        if (routeName.Equals("ProductTagsAll", StringComparison.OrdinalIgnoreCase) ||
            routeName.Equals("ProductsByTag", StringComparison.OrdinalIgnoreCase) ||
            routeName.Equals("BlogByTag", StringComparison.OrdinalIgnoreCase) ||
            (controller?.Equals("Blog", StringComparison.OrdinalIgnoreCase) == true &&
             action?.Equals("BlogByTag", StringComparison.OrdinalIgnoreCase) == true))
        {
            eventMessage.Helper.AddHeadCustomParts("<meta name=\"robots\" content=\"noindex,follow\" />");
        }
    }

    private static bool IsCanonicalPublicAction(string controller, string action)
    {
        return controller?.Equals("Home", StringComparison.OrdinalIgnoreCase) == true ||
               controller?.Equals("Blog", StringComparison.OrdinalIgnoreCase) == true ||
               controller?.Equals("Common", StringComparison.OrdinalIgnoreCase) == true &&
               action?.Equals("Sitemap", StringComparison.OrdinalIgnoreCase) == true;
    }
}
