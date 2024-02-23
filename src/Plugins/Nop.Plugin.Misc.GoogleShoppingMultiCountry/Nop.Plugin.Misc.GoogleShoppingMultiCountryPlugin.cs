using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nop.Services.Cms;
using Nop.Services.Common;
using Nop.Services.Plugins;
using Nop.Web.Framework.Infrastructure;

namespace Nop.Plugin.Misc.GoogleShoppingMultiCountry
{
    /// <summary>
    /// Rename this file and change to the correct type
    /// </summary>
    public class GoogleShoppingMultiCountry : BasePlugin, IMiscPlugin
    {
        public GoogleShoppingMultiCountry()
        {

        }
        // <summary>
        /// Install plugin
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
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
        /// <summary>
        /// Gets a configuration page URL
        /// </summary>
        public override string GetConfigurationPageUrl()
        {
            //return _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext).RouteUrl(AccessiBeDefaults.ConfigurationRouteName);
            //return $"{_webHelper.GetStoreLocation()}Admin/PaymentIyzico/Configure";
            return "";
        }

    }
}
