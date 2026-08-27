using Microsoft.AspNetCore.Mvc.Razor;

namespace Nop.Plugin.Widgets.HoodVideoSeo.Infrastructure;

public sealed class VideoSeoViewLocationExpander : IViewLocationExpander
{
    public void PopulateValues(ViewLocationExpanderContext context)
    {
    }

    public IEnumerable<string> ExpandViewLocations(ViewLocationExpanderContext context, IEnumerable<string> viewLocations)
    {
        if (string.Equals(context.AreaName, "Admin", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(context.ControllerName, "Product", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(context.ViewName, "_ProductDetailsVideos", StringComparison.OrdinalIgnoreCase))
            return viewLocations;

        return new[]
        {
            "/Plugins/Widgets.HoodVideoSeo/Views/Product/_ProductDetailsVideos.cshtml"
        }.Concat(viewLocations);
    }
}
