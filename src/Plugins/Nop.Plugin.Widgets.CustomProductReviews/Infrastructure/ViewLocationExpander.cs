using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Razor;
using Nop.Web.Framework.Themes;
using Nop.Web.Framework;

namespace Nop.Plugin.Widgets.CustomProductReviews.Infrastructure
{
    public class ViewLocationExpander : IViewLocationExpander
    {
        private const string THEME_KEY = "nop.themename";
       
        public void PopulateValues(ViewLocationExpanderContext context)
        {
            if (context.AreaName?.Equals(AreaNames.ADMIN) ?? false)
                return;

            var themeContext = context.ActionContext.HttpContext.RequestServices
                .GetService(typeof(IThemeContext)) as IThemeContext;

            if (themeContext != null)
                context.Values[THEME_KEY] = themeContext.GetWorkingThemeNameAsync().GetAwaiter().GetResult();
        }

        public IEnumerable<string> ExpandViewLocations(ViewLocationExpanderContext context, IEnumerable<string> viewLocations)
        {
            

            if (context.AreaName == "Admin")
            {
                // Admin area için plugin view'larını yükle
                viewLocations = new[] { $"/Plugins/Widgets.CustomProductReviews/Areas/Admin/Views/{context.ControllerName}/{context.ViewName}.cshtml" }.Concat(viewLocations);
            }
            else
            {
                // Public area için plugin view'larını temanın view'larından önce yükle
                if (!context.Values.TryGetValue(THEME_KEY, out string theme))
                    return viewLocations;

                if (context.ViewName == "_ProductReviews")
                {
                    // The action belongs to CustomProductReviewsController, but the
                    // partial is intentionally stored under Views/Product.
                    viewLocations = new[]
                    {
                        $"~/Plugins/Widgets.CustomProductReviews/Themes/{theme}/Views/Product/{{0}}.cshtml",
                        "~/Plugins/Widgets.CustomProductReviews/Views/Product/{0}.cshtml",
                        $"~/Plugins/Widgets.CustomProductReviews/Themes/{theme}/Views/{{1}}/{{0}}.cshtml",
                        "~/Plugins/Widgets.CustomProductReviews/Views/{1}/{0}.cshtml",
                        $"~/Themes/{theme}/Views/{{1}}/{{0}}.cshtml"
                    }.Concat(viewLocations);
                }
            }

            return viewLocations;
        }
       


    }
}
