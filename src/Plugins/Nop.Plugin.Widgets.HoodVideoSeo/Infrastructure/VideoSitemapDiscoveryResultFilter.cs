using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Nop.Core;

namespace Nop.Plugin.Widgets.HoodVideoSeo.Infrastructure;

/// <summary>
/// Adds the plugin-owned video sitemap to nopCommerce's generated robots.txt
/// without replacing the core controller, factory, or physical robots files.
/// </summary>
public sealed class VideoSitemapDiscoveryResultFilter : IAsyncResultFilter
{
    private const string SitemapPath = "video-sitemap.xml";
    private readonly IWebHelper _webHelper;

    public VideoSitemapDiscoveryResultFilter(IWebHelper webHelper)
    {
        _webHelper = webHelper;
    }

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        var routeValues = context.ActionDescriptor.RouteValues;
        routeValues.TryGetValue("controller", out var controller);
        routeValues.TryGetValue("action", out var action);
        if (context.Result is ContentResult content &&
            string.Equals(controller, "Common", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(action, "RobotsTextFile", StringComparison.OrdinalIgnoreCase))
        {
            var sitemapUrl = $"{_webHelper.GetStoreLocation().TrimEnd('/')}/{SitemapPath}";
            var directive = $"Sitemap: {sitemapUrl}";
            if (!(content.Content ?? string.Empty).Contains(directive, StringComparison.OrdinalIgnoreCase))
                content.Content = $"{(content.Content ?? string.Empty).TrimEnd()}\r\n{directive}\r\n";
        }

        await next();
    }
}
