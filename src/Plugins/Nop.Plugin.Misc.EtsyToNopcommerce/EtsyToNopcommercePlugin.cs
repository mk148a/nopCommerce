using System;
using System.Collections.Generic;
using Nop.Core.Http;
using Nop.Core;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Extensions.Logging;
using Nop.Services.Cms;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Plugins;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Nop.Services.Common;

namespace Nop.Plugin.Misc.EtsyToNopcommerce
{
    /// <summary>
    /// Rename this file and change to the correct type
    /// </summary>
    public class EtsyToNopcommercePlugin : BasePlugin, IMiscPlugin
    {
        #region Fields

        private readonly ILocalizationService _localizationService;
  
        private readonly ISettingService _settingService;
        private readonly IUrlHelperFactory _urlHelperFactory;
        private readonly IActionContextAccessor _actionContextAccessor;

        #endregion

        #region Ctor

        public EtsyToNopcommercePlugin(ILocalizationService localizationService,
            ISettingService settingService, IUrlHelperFactory urlHelperFactory, IActionContextAccessor actionContextAccessor)
        {
          
            _localizationService = localizationService;
           
            _settingService = settingService;
            _urlHelperFactory = urlHelperFactory;
            _actionContextAccessor = actionContextAccessor;
        }

        #endregion

        #region Methods

    
        public override async Task InstallAsync()
        {
            //settings
            var defaultSettings = new EtsyToNopcommerceSettings
            {
                ConsumerKey = "uh3pwbu285jcynbsz50cadww",
                ConsumerSecret = "j6kxqxmu8c",
                RequestUrl= "https://www.etsy.com/oauth/connect",
                RequestAccessTokenUrl = "https://openapi.etsy.com/v3/public/oauth/token"
            };
            await _settingService.SaveSettingAsync(defaultSettings);

            //locales
            await _localizationService.AddOrUpdateLocaleResourceAsync("Nop.Plugins.Misc.EtsyToNopcommerce.Test", "Test");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Nop.Plugins.Misc.EtsyToNopcommerce.Fields.AuthorizeNecessary", "Etsy Mağazanız İçin Yetkilendirme Gerekiyor");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Nop.Plugins.Misc.EtsyToNopcommerce.Fields.AuthorizeSuccess", "Etsy Mağazanız İçin Yetkilendirme Başarılı");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Nop.Plugin.Misc.EtsyToNopcommerce.Fields.ShopName", "Etsy Mağaza Adı");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Nop.Plugin.Misc.EtsyToNopcommerce.Fields.ShopId", "Etsy Mağaza Id");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Nop.Plugin.Misc.EtsyToNopcommerce.Fields.ConsumerKey", "Etsy Api Key");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Nop.Plugin.Misc.EtsyToNopcommerce.Fields.RefreshToken", "RefreshToken");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Nop.Plugin.Misc.EtsyToNopcommerce.Fields.TokenDate", "Token Alınma Tarihi");

            await base.InstallAsync();
        }

        /// <summary>
        /// Uninstall the plugin
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
        public override async Task UninstallAsync()
        {
            //settings
            await _settingService.DeleteSettingAsync<EtsyToNopcommerceSettings>();

            //locales
            await _localizationService.DeleteLocaleResourceAsync("Nop.Plugins.Misc.EtsyToNopcommerce.Test");
            await _localizationService.DeleteLocaleResourceAsync("Nop.Plugins.Misc.EtsyToNopcommerce.Fields.AuthorizeNecessary");
            await _localizationService.DeleteLocaleResourceAsync("Nop.Plugins.Misc.EtsyToNopcommerce.Fields.AuthorizeSuccess");
            await _localizationService.DeleteLocaleResourceAsync("Nop.Plugin.Misc.EtsyToNopcommerce.Fields.ShopName");
            await _localizationService.DeleteLocaleResourceAsync("Nop.Plugin.Misc.EtsyToNopcommerce.Fields.ShopId");
            await _localizationService.DeleteLocaleResourceAsync("Nop.Plugin.Misc.EtsyToNopcommerce.Fields.ConsumerKey");
            await _localizationService.DeleteLocaleResourceAsync("Nop.Plugin.Misc.EtsyToNopcommerce.Fields.RefreshToken");
            await _localizationService.DeleteLocaleResourceAsync("Nop.Plugin.Misc.EtsyToNopcommerce.Fields.TokenDate");


            await base.UninstallAsync();
        }

        public override string GetConfigurationPageUrl()
        {
            return _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext).RouteUrl("Plugin.Misc.EtsyToNopcommerce.Configure");
        }

        #endregion

    }
}
