using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Policy;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.Drawing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Nop.Core;
using Nop.Core.Domain.Cms;
using Nop.Core.Infrastructure;
using Nop.Services.Cms;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Plugins;
using Nop.Services.Stores;
using Nop.Web.Framework.Infrastructure;
namespace Nop.Plugin.Widgets.NopQuickTabsBugFix
{
    /// <summary>
    /// Rename this file and change to the correct type
    /// </summary>
    public class NopQuickTabsBugFixPlugin : BasePlugin, IWidgetPlugin
    {
      

        bool IWidgetPlugin.HideInWidgetList => false;

        public string GetWidgetViewComponentName(string widgetZone)
        {
            if (widgetZone == null)
                throw new ArgumentNullException(nameof(widgetZone));

            return "NopQuickTabsBugFix";
        }

        public async Task<IList<string>> GetWidgetZonesAsync()
        {
            return await Task.FromResult<IList<string>>(new List<string> {  });

        }

        public NopQuickTabsBugFixPlugin()
        {

        }
        #region Methods








        /// <summary>
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

        #endregion

    }


}
