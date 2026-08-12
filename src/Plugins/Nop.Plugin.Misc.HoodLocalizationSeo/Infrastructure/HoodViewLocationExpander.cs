using Microsoft.AspNetCore.Mvc.Razor;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Infrastructure;

public sealed class HoodViewLocationExpander : IViewLocationExpander
{
    public void PopulateValues(ViewLocationExpanderContext context)
    {
    }

    public IEnumerable<string> ExpandViewLocations(ViewLocationExpanderContext context,
        IEnumerable<string> viewLocations)
    {
        if (context.AreaName?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true)
            return viewLocations;

        return new[]
        {
            "/Plugins/Nop.Plugin.Misc.HoodLocalizationSeo/Views/{1}/{0}.cshtml",
            "/Plugins/Nop.Plugin.Misc.HoodLocalizationSeo/Views/Shared/{0}.cshtml"
        }.Concat(viewLocations);
    }
}
