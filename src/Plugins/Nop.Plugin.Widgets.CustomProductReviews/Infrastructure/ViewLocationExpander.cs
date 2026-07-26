using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.DependencyInjection;
using Nop.Web.Framework;
using Nop.Web.Framework.Themes;

namespace Nop.Plugin.Widgets.CustomProductReviews.Infrastructure
{
    public class ViewLocationExpander : IViewLocationExpander
    {
        private const string ThemeKey = "nop.themename";

        public void PopulateValues(ViewLocationExpanderContext context)
        {
            if (context.AreaName?.Equals(AreaNames.ADMIN) ?? false)
                return;

            var themeContext = context.ActionContext.HttpContext.RequestServices.GetService<IThemeContext>();
            if (themeContext == null)
                return;

            var themeName = themeContext.GetWorkingThemeNameAsync().GetAwaiter().GetResult();
            if (!string.IsNullOrWhiteSpace(themeName))
                context.Values[ThemeKey] = themeName;
        }

        public IEnumerable<string> ExpandViewLocations(
            ViewLocationExpanderContext context,
            IEnumerable<string> viewLocations)
        {
            if (context.AreaName?.Equals(AreaNames.ADMIN) ?? false)
            {
                return new[]
                {
                    "/Plugins/Widgets.CustomProductReviews/Areas/Admin/Views/{1}/{0}.cshtml"
                }.Concat(viewLocations);
            }

            if (!string.Equals(context.ViewName, "_ProductReviews", System.StringComparison.Ordinal))
                return viewLocations;

            var pluginLocations = new List<string>();

            if (context.Values.TryGetValue(ThemeKey, out var theme) &&
                !string.IsNullOrWhiteSpace(theme))
            {
                pluginLocations.Add(
                    $"~/Plugins/Widgets.CustomProductReviews/Themes/{theme}/Views/Product/{{0}}.cshtml");
            }

            pluginLocations.Add(
                "~/Plugins/Widgets.CustomProductReviews/Views/Product/{0}.cshtml");

            pluginLocations.Add(
                "~/Plugins/Widgets.CustomProductReviews/Views/{1}/{0}.cshtml");

            return pluginLocations.Concat(viewLocations);
        }
    }
}
