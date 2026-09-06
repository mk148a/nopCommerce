using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Misc.HoodSitemapCompatibility.Infrastructure;

/// <summary>
/// Maps the known obsolete sitemap partition before the core sitemap route.
/// </summary>
public sealed class LegacySitemapRouteProvider : IRouteProvider
{
    public void RegisterRoutes(IEndpointRouteBuilder endpointRouteBuilder)
    {
        // These were submitted to Search Console by a previous multi-part sitemap
        // build. The current site exposes one valid sitemap only; if the core route
        // handles these it attempts to serve deleted physical files and returns 500.
        foreach (var legacyPartition in new[] { 2, 3, 4 })
        {
            endpointRouteBuilder.MapControllerRoute(
                name: $"Plugin.Misc.HoodSitemapCompatibility.Sitemap{legacyPartition}",
                pattern: $"sitemap-{legacyPartition}.xml",
                defaults: new { controller = "LegacySitemap", action = "LegacyPartition" });
        }

        endpointRouteBuilder.MapControllerRoute(
            name: "Plugin.Misc.HoodSitemapCompatibility.SitemapNine",
            pattern: "sitemap-9.xml",
            defaults: new { controller = "LegacySitemap", action = "SitemapNine" });
    }

    public int Priority => 10000;
}
