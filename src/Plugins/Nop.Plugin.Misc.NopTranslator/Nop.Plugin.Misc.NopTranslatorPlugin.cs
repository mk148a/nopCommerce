using System;
using System.Collections.Generic;
using Nop.Core.Domain.Cms;
using System.Linq;
using System.Threading.Tasks;
using Nop.Services.Cms;
using Nop.Services.Plugins;
using Nop.Web.Framework.Infrastructure;
using Nop.Core;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Stores;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Nop.Plugin.Misc.NopTranslator
{
    /// <summary>
    /// Rename this file and change to the correct type
    /// </summary>
    public class NopTranslatorPlugin : BasePlugin
    {
        #region Fields

        private readonly NopTranslatorSettings _nopTranslatorSettings;
        private readonly IActionContextAccessor _actionContextAccessor;
        private readonly ILocalizationService _localizationService;
        private readonly ISettingService _settingService;
        private readonly IStoreService _storeService;
        private readonly IStoreContext _storeContext;
        private readonly IUrlHelperFactory _urlHelperFactory;
        private readonly IWebHelper _webHelper;
        private IPluginsInfo _pluginsInfo;
        private readonly IPluginService _pluginService;
        private readonly IHttpContextAccessor _httpContextAccessor;



        #endregion

        #region Ctor

        public NopTranslatorPlugin(NopTranslatorSettings customProdutReviewSettings,
            IActionContextAccessor actionContextAccessor,
            ILocalizationService localizationService,
            ISettingService settingService,
            IStoreService storeService,
            IUrlHelperFactory urlHelperFactory, IStoreContext storeContext, IWebHelper webHelper, IPluginService pluginService, IHttpContextAccessor httpContextAccessor)
        {
            _nopTranslatorSettings = customProdutReviewSettings;
            _actionContextAccessor = actionContextAccessor;
            _localizationService = localizationService;
            _settingService = settingService;
            _storeService = storeService;
            _urlHelperFactory = urlHelperFactory;
            _storeContext = storeContext;
            _webHelper = webHelper;
            _pluginService = pluginService;
            _httpContextAccessor = httpContextAccessor;




        }

        #endregion

        #region Methods

        /// <summary>
        /// Gets a configuration page URL
        /// </summary>
        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation()}Admin/NopTranslator/Configure";
           
        }



        /// <summary>
        /// Install plugin
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
        public override async Task InstallAsync()
        {

            try
            {
                

                //await _settingService.SaveSettingAsync(new NopTranslatorSettings
                //{
                //    WidgetZone = PublicWidgetZones.ProductReviewsPageTop,
                //    data = "json",
                //    MaximumFile = 5,
                //    MaximumSize = 1073741824
                //});




                await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
                {
                    ["Plugins.Misc.NopTranslator.Fields.Enabled"] = "Enable",
                    ["Plugins.Misc.NopTranslator.Fields.Enabled.Hint"] = "Check to activate this widget.",
                    ["Plugins.Misc.NopTranslator.Fields.Script"] = "Installation script",
                    ["Plugins.Misc.NopTranslator.Fields.Script.Hint"] =
                        "Find your unique installation script on the Installation tab in your account and then copy it into this field.",
                    ["Plugins.Misc.NopTranslator.Fields.Script.Required"] =
                        "Installation script is required",
                });

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
            //await _settingService.DeleteSettingAsync<NopTranslatorSettings>();

            //var stores = await _storeService.GetAllStoresAsync();
            //var storeIds = new List<int> { 0 }.Union(stores.Select(store => store.Id));
            //foreach (var storeId in storeIds)
            //{
            //    var widgetSettings = await _settingService.LoadSettingAsync<WidgetSettings>(storeId);
            //    widgetSettings.ActiveWidgetSystemNames.Remove(CustomProductReviewsDefaults.SystemName);
            //    await _settingService.SaveSettingAsync(widgetSettings);
            //}

            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Misc.NopTranslator");

            await base.UninstallAsync();
        }

        #endregion
    }
}
