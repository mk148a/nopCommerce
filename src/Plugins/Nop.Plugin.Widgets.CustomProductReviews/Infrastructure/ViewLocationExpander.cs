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
       
        public async void PopulateValues(ViewLocationExpanderContext context)
        {
            if (context.AreaName?.Equals(AreaNames.ADMIN) ?? false)
                return;

            var themeContext = (IThemeContext)context.ActionContext.HttpContext.RequestServices.GetService(typeof(IThemeContext));
            context.Values[THEME_KEY] =await themeContext.GetWorkingThemeNameAsync();
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
                    // Plugin view'larını temanın view'larından önce yükle
                    viewLocations = new[] {
                        $"~/Plugins/Widgets.CustomProductReviews/Views/{{{1}}}/{{{0}}}.cshtml", // Plugin view'ı
                        $"~/Plugins/Widgets.CustomProductReviews/Themes/{theme}/Views/{{{1}}}/{{{0}}}.cshtml", // Tema özel view'ı
                        $"~/Themes/{theme}/Views/{{{{1}}}}/{{{{0}}}}.cshtml" // Tema view'ı
                    }.Concat(viewLocations);
                }
            }

            return viewLocations;
        }
       


    }
}
