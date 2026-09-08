using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Widgets.HoodVideoSeo.Infrastructure;

/// <summary>
/// Provides a crawlable, localized page where a YouTube product video is the main content.
/// </summary>
public sealed class VideoWatchRouteProvider : IRouteProvider
{
    public void RegisterRoutes(IEndpointRouteBuilder endpointRouteBuilder)
    {
        endpointRouteBuilder.MapControllerRoute(
            name: "Plugin.Widgets.HoodVideoSeo.Watch",
            pattern: "{language}/watch/{productId:int}/{youtubeId}",
            defaults: new { controller = "VideoWatch", action = "Watch" });

        endpointRouteBuilder.MapControllerRoute(
            name: "Plugin.Widgets.HoodVideoSeo.RootSitemap",
            pattern: "video-sitemap.xml",
            defaults: new { controller = "VideoWatch", action = "ProductVideos" });

        // Preserve the first rollout route as a compatibility alias. Crawlers are
        // directed to the stable root URL through robots.txt.
        endpointRouteBuilder.MapControllerRoute(
            name: "Plugin.Widgets.HoodVideoSeo.LegacySitemap",
            pattern: "{language}/video/sitemap",
            defaults: new { controller = "VideoWatch", action = "ProductVideos" });
    }

    public int Priority => 100;
}
