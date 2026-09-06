using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;
using Nop.Web.Infrastructure;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Infrastructure;

public sealed class HoodRouteProvider : BaseRouteProvider, IRouteProvider
{
    internal const string IndexedSitemapRoutePattern = "sitemap-{id:int:min(1)}.xml";
    internal const int RoutePriority = 10000;

    public void RegisterRoutes(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var language = GetLanguageRoutePattern();

        // Guard numbered sitemap requests before the core route. The core
        // factory can return a path for an out-of-range part without creating
        // that file, which otherwise fails later while executing PhysicalFile.
        // sitemap.xml intentionally remains owned by the core route.
        endpointRouteBuilder.MapControllerRoute(name: "HoodIndexedSitemapXml",
            pattern: IndexedSitemapRoutePattern,
            defaults: new { controller = "HoodLocalization", action = "SitemapXml" });

        // Same route name/pattern as core, registered with higher priority so
        // existing RSS links transparently use localized plugin output.
        endpointRouteBuilder.MapControllerRoute(name: "HoodBlogRSS",
            pattern: "blog/rss/{languageId:min(0)}",
            defaults: new { controller = "HoodLocalization", action = "BlogRss" });

        // GET/HEAD requests to the historical fixed contact route are moved to
        // the active localized Topic slug. POST continues to be handled by the
        // core Common.ContactUs action through HTTP action constraints.
        endpointRouteBuilder.MapControllerRoute(name: "HoodLegacyContactUs",
            pattern: $"{language}/contactus",
            defaults: new { controller = "HoodLocalization", action = "RedirectLegacyContactUs" });
    }

    public int Priority => RoutePriority;
}
