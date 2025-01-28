using System;
using System.Collections.Generic;
using Nop.Core.Domain.Cms;
using System.Linq;
using System.Threading.Tasks;
using Nop.Services.Cms;
using Nop.Services.Plugins;
using Nop.Web.Framework.Infrastructure;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Nop.Core.Events;
using Nop.Core.Infrastructure;
using Nop.Core;
using Nop.Services.Localization;
using Nop.Web.Areas.Admin.Models.Catalog;
using System.Text;
using Microsoft.AspNetCore.Mvc.Razor;
using Nop.Core.Domain.Catalog;
using System.Diagnostics.Tracing;
using Nop.Web.Framework.Mvc.Routing;
using Microsoft.AspNetCore.Routing;

namespace Nop.Plugin.Misc.GoogleMultiLanguageAndCurrency
{
    /// <summary>
    /// Rename this file and change to the correct type
    /// </summary>
    public class GoogleMultiLanguageAndCurrencyPlugin : BasePlugin, IWidgetPlugin
    {


        public bool HideInWidgetList => false;

        public GoogleMultiLanguageAndCurrencyPlugin()
        {
           
        }
        
        public override async Task InstallAsync()
        {

            try
            {
           
                await base.InstallAsync();
          
            }
            catch (Exception e)
            {
                Console.WriteLine(e);

            }
        }

        /// <summary>
        /// Uninstall plugin
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
        public override async Task UninstallAsync()
        {
            await base.UninstallAsync();
        }

        string IWidgetPlugin.GetWidgetViewComponentName(string widgetZone)
        {
            // Widget adınızı belirtin (örneğin, 'GoogleMultiLanguageAndCurrencyWidget')
            return "GoogleMultiLanguageAndCurrencyWidget";
        }

        async Task<IList<string>> IWidgetPlugin.GetWidgetZonesAsync()
        {
            return await Task.FromResult<IList<string>>(new List<string> { PublicWidgetZones.HeadHtmlTag });
        }

     
    }

    
}
