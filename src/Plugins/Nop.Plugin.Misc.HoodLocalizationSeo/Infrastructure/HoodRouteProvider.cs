using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;
using Nop.Web.Infrastructure;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Infrastructure;

public sealed class HoodRouteProvider : BaseRouteProvider, IRouteProvider
{
    internal const string RootSitemapRoutePattern = "sitemap.xml";
    internal const string IndexedSitemapRoutePattern = "sitemap-{id:int:min(1)}.xml";
    internal const int RoutePriority = 10000;

    public void RegisterRoutes(IEndpointRouteBuilder endpointRouteBuilder)
    {
        RegisterSitemapRoutes(endpointRouteBuilder);

        var language = GetLanguageRoutePattern();

        // Same route name/pattern as core, registered with higher priority so
        // existing RSS links transparently use localized plugin output.
        endpointRouteBuilder.MapControllerRoute(name: "HoodBlogRSS",
            pattern: "blog/rss/{languageId:min(0)}",
            defaults: new { controller = "HoodLocalization", action = "BlogRss" });

        endpointRouteBuilder.MapControllerRoute(name: "HoodHalloweenLanding",
            pattern: $"{language}/{HalloweenLandingRoute.Slug}",
            defaults: new { controller = "HalloweenLanding", action = "HalloweenLanding" });

        // GET/HEAD requests to the historical fixed contact route are moved to
        // the active localized Topic slug. POST continues to be handled by the
        // core Common.ContactUs action through HTTP action constraints.
        endpointRouteBuilder.MapControllerRoute(name: "HoodLegacyContactUs",
            pattern: $"{language}/contactus",
            defaults: new { controller = "HoodLocalization", action = "RedirectLegacyContactUs" });
    }

    internal static void RegisterSitemapRoutes(IEndpointRouteBuilder endpointRouteBuilder)
    {
        // Serve both public sitemap forms before the core routes so the same
        // validated source can receive the plugin-owned streaming hreflang
        // correction without changing the cached core XML artifact.
        endpointRouteBuilder.MapControllerRoute(name: "HoodSitemapXml",
            pattern: RootSitemapRoutePattern,
            defaults: new { controller = "HoodLocalization", action = "SitemapXml", id = 0 });

        // Guard numbered sitemap requests before the core route. The core
        // factory can return a path for an out-of-range part without creating
        // that file, which otherwise fails later while executing PhysicalFile.
        endpointRouteBuilder.MapControllerRoute(name: "HoodIndexedSitemapXml",
            pattern: IndexedSitemapRoutePattern,
            defaults: new { controller = "HoodLocalization", action = "SitemapXml" });
    }

    public int Priority => RoutePriority;
}
