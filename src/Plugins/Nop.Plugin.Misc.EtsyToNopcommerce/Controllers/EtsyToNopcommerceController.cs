using System;
using System.Linq;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.EMMA;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Misc.EtsyToNopcommerce;
using Nop.Plugin.Misc.EtsyToNopcommerce.Models;
using Nop.Services;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Payments.PayPalCommerce.Controllers
{
    [Area(AreaNames.Admin)]
    [AutoValidateAntiforgeryToken]
    [ValidateIpAddress]
    [AuthorizeAdmin]
    public class EtsyToNopcommerceController : BasePluginController
    {
        #region Fields

        private readonly ILocalizationService _localizationService;
        private readonly INotificationService _notificationService;
        private readonly IPermissionService _permissionService;
        private readonly ISettingService _settingService;
        private readonly IStoreContext _storeContext;
        //private readonly ServiceManager _serviceManager;
        private readonly ShoppingCartSettings _shoppingCartSettings;

        #endregion

        #region Ctor

        public EtsyToNopcommerceController(ILocalizationService localizationService,
            INotificationService notificationService,
            IPermissionService permissionService,
            ISettingService settingService,
            IStoreContext storeContext,
            ShoppingCartSettings shoppingCartSettings)
        {
            _localizationService = localizationService;
            _notificationService = notificationService;
            _permissionService = permissionService;
            _settingService = settingService;
            _storeContext = storeContext;
            _shoppingCartSettings = shoppingCartSettings;
        }

        #endregion

    

        #region Methods

        public async Task<IActionResult> Configure()
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.AccessAdminPanel))
                return AccessDeniedView();

            var storeId = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var settings = await _settingService.LoadSettingAsync<EtsyToNopcommerceSettings>(storeId);


            var model = new ConfigurationModel
            {
                ShopName = settings.ShopName,
                ShopId = settings.ShopId,
                RequestUrl = settings.RequestUrl,
                RequestAccessTokenUrl = settings.RequestAccessTokenUrl,
                ConsumerKey = settings.ConsumerKey,
                ConsumerSecret = settings.ConsumerSecret,
                Token = settings.Token,
                TokenSecret = settings.TokenSecret,
                RefreshToken = settings.RefreshToken,
                TokenDate = settings.TokenDate,
                ActiveStoreScopeConfiguration = storeId

            };

            //we don't need some of the shared settings that loaded above, so load them separately for chosen store
            if (storeId > 0)
            {
                //settings.WebhookUrl = await _settingService
                //    .GetSettingByKeyAsync<string>($"{nameof(PayPalCommerceSettings)}.{nameof(PayPalCommerceSettings.WebhookUrl)}", storeId: storeId);
                //settings.UseSandbox = await _settingService
                //    .GetSettingByKeyAsync<bool>($"{nameof(PayPalCommerceSettings)}.{nameof(PayPalCommerceSettings.UseSandbox)}", storeId: storeId);
                //settings.ClientId = await _settingService
                //    .GetSettingByKeyAsync<string>($"{nameof(PayPalCommerceSettings)}.{nameof(PayPalCommerceSettings.ClientId)}", storeId: storeId);
                //settings.SecretKey = await _settingService
                //    .GetSettingByKeyAsync<string>($"{nameof(PayPalCommerceSettings)}.{nameof(PayPalCommerceSettings.SecretKey)}", storeId: storeId);
                //settings.Email = await _settingService
                //    .GetSettingByKeyAsync<string>($"{nameof(PayPalCommerceSettings)}.{nameof(PayPalCommerceSettings.Email)}", storeId: storeId);
                //settings.MerchantGuid = await _settingService
                //    .GetSettingByKeyAsync<string>($"{nameof(PayPalCommerceSettings)}.{nameof(PayPalCommerceSettings.MerchantGuid)}", storeId: storeId);
                //settings.SignUpUrl = await _settingService
                //    .GetSettingByKeyAsync<string>($"{nameof(PayPalCommerceSettings)}.{nameof(PayPalCommerceSettings.SignUpUrl)}", storeId: storeId);
            }



            //ensure credentials are valid
            //if (!string.IsNullOrEmpty(settings.ClientId) && !string.IsNullOrEmpty(settings.SecretKey))
            //{
            //    var (_, credentialsError) = await _serviceManager.GetAccessTokenAsync(settings);
            //    if (!string.IsNullOrEmpty(credentialsError))
            //        _notificationService.ErrorNotification(await _localizationService.GetResourceAsync("Plugins.Payments.PayPalCommerce.Credentials.Invalid"));
            //    else
            //        _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Plugins.Payments.PayPalCommerce.Credentials.Valid"));
            //}


           

            return View("~/Plugins/Nop.Plugin.Misc.EtsyToNopcommerce/Views/Configure.cshtml", model);
         
        }

        [HttpPost, ActionName("Configure")]
        [FormValueRequired("save")]
        public async Task<IActionResult> Configure(ConfigurationModel model)
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            var storeId = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var settings = await _settingService.LoadSettingAsync<EtsyToNopcommerceSettings>(storeId);

            //set new settings values
            settings.RequestAccessTokenUrl = model.RequestAccessTokenUrl;
            settings.ConsumerKey = model.ConsumerKey;
            settings.ConsumerSecret = model.ConsumerSecret;
            settings.Token = model.Token;
            settings.TokenSecret = model.TokenSecret;
            settings.RefreshToken = model.RefreshToken;
            settings.ShopName=model.ShopName;
            settings.ShopId=model.ShopId;
            settings.RequestUrl=model.RequestUrl;
            settings.TokenDate=model.TokenDate;
            
            //settings.PaymentType = (PaymentType)model.PaymentTypeId;
            //settings.DisplayButtonsOnShoppingCart = model.DisplayButtonsOnShoppingCart;
            //settings.DisplayButtonsOnProductDetails = model.DisplayButtonsOnProductDetails;
            //settings.DisplayLogoInHeaderLinks = model.DisplayLogoInHeaderLinks;
            //settings.LogoInHeaderLinks = model.LogoInHeaderLinks;
            //settings.DisplayLogoInFooter = model.DisplayLogoInFooter;
            //settings.DisplayPayLaterMessages = model.DisplayPayLaterMessages;
            //settings.LogoInFooter = model.LogoInFooter;

         

            //await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.SetCredentialsManually, model.SetCredentialsManually_OverrideForStore, storeId, false);
            //await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.PaymentType, model.PaymentTypeId_OverrideForStore, storeId, false);
            //await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.DisplayButtonsOnShoppingCart, model.DisplayButtonsOnShoppingCart_OverrideForStore, storeId, false);
            //await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.DisplayButtonsOnProductDetails, model.DisplayButtonsOnProductDetails_OverrideForStore, storeId, false);
            //await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.DisplayLogoInHeaderLinks, model.DisplayLogoInHeaderLinks_OverrideForStore, storeId, false);
            //await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.LogoInHeaderLinks, model.LogoInHeaderLinks_OverrideForStore, storeId, false);
            //await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.DisplayLogoInFooter, model.DisplayLogoInFooter_OverrideForStore, storeId, false);
            //await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.DisplayPayLaterMessages, model.DisplayPayLaterMessages_OverrideForStore, storeId, false);
            //await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.LogoInFooter, model.LogoInFooter_OverrideForStore, storeId, false);
            await _settingService.SaveSettingAsync(settings);
            await _settingService.ClearCacheAsync();

            _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

            return await Configure();
        }


        #endregion
    }
}