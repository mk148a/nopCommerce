using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Text;
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
using System.Net;
using RestSharp;
using Nop.Plugin.Misc.EtsyToNopcommerce.Models.Shops;

namespace Nop.Plugin.Misc.EtsyToNopcommerce.Controllers
{
    [AuthorizeAdmin]
    [Area(AreaNames.Admin)]
    [AutoValidateAntiforgeryToken]
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
                model.ShopName_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.ShopName, storeId);
                model.ShopId_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.ShopId, storeId);
                model.RequestUrl_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.RequestUrl, storeId);
                model.RequestAccessTokenUrl_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.RequestAccessTokenUrl, storeId);
                model.ConsumerKey_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.ConsumerKey, storeId);
                model.ConsumerSecret_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.ConsumerSecret, storeId);
                model.RefreshToken_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.RefreshToken, storeId);
                model.TokenSecret_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.TokenSecret, storeId);
                model.TokenDate_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.TokenDate, storeId);
               
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

        private string GenerateCodeChallenge(string codeVerifier)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(codeVerifier));
            var b64Hash = Convert.ToBase64String(hash);
            var code = Regex.Replace(b64Hash, "\\+", "-");
            code = Regex.Replace(code, "\\/", "_");
            code = Regex.Replace(code, "=+$", "");
            return code;
        }

        [HttpGet]
        public async Task<IActionResult> Yetkilendir()
        {
            var storeId = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var settings = await _settingService.LoadSettingAsync<EtsyToNopcommerceSettings>(storeId);

         
            if (settings != null)
            {
                string code_challenge = GenerateCodeChallenge(settings.ConsumerKey);

                //https://localhost:59857/etys-yetkilendir
                string callbackUrl = $"{this.Request.Scheme}://{this.Request.Host}" + "/Admin/EtsyToNopcommerce/etys-yetkilendir";

                string url = $"{settings.RequestUrl}?response_type=code&redirect_uri={callbackUrl}&scope=address_r%20address_w%20billing_r%20cart_r%20cart_w%20email_r%20favorites_r%20favorites_w%20feedback_r%20listings_d%20listings_r%20listings_w%20profile_r%20profile_w%20recommend_r%20recommend_w%20shops_r%20shops_w%20transactions_r%20transactions_w&client_id={settings.ConsumerKey}&state=superstate&code_challenge={code_challenge}&code_challenge_method=S256";
                return Redirect(url);
            }
            else
            {
               
                return await Configure();
            }
        }



        [HttpGet]
        public async Task<IActionResult> CallbackAsync()
        {
            var storeId = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var settings = await _settingService.LoadSettingAsync<EtsyToNopcommerceSettings>(storeId);

            if (settings != null)
            {
                string RequestAccessTokenUrl = settings.RequestAccessTokenUrl;
                string ConsumerKey = settings.ConsumerKey;
                string ConsumerSecret = settings.ConsumerSecret;
                string TokenSecret = "";
                // Read token and verifier
                string code = Request.Query["code"];


                string redirect_uri = $"{this.Request.Scheme}://{this.Request.Host}" + "/etys-yetkilendir";
                RestClient RestClient = new RestSharp.RestClient("https://openapi.etsy.com");
                var request = new RestRequest("/v3/public/oauth/token",RestSharp.Method.Post);
                request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
                request.AddParameter("grant_type", "authorization_code");
                request.AddParameter("client_id", ConsumerKey);
                request.AddParameter("redirect_uri", redirect_uri);
                request.AddParameter("code", code);
                request.AddParameter("code_verifier", ConsumerKey);

                var response = await RestClient.ExecutePostAsync(request);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var AuthorizationResponse = Newtonsoft.Json.JsonConvert.DeserializeObject<AuthorizationResponse>(response.Content);
                    settings.Token = AuthorizationResponse.access_token;
                    settings.RefreshToken = AuthorizationResponse.refresh_token;
                    settings.ExpiresIn = AuthorizationResponse.expires_in;
                    settings.TokenDate = DateTime.Now;




                    //Shop id al
                    redirect_uri = $"{this.Request.Scheme}://{this.Request.Host}" + "/etys-yetkilendir";
                    RestClient RestClient1 = new RestSharp.RestClient("https://openapi.etsy.com");
                    RestRequest request1 = new RestRequest("/v3/application/shops");
                    request1.AddHeader("Content-Type", "application/x-www-form-urlencoded");
                    request1.AddHeader("x-api-key", "uh3pwbu285jcynbsz50cadww");
                    request1.AddHeader("Authorization", "Bearer " + settings.Token);
                    request1.AddParameter("shop_name", settings.ShopName);

                    var response1 = await RestClient1.ExecuteGetAsync(request1);
                    string content = response1.Content;
                    settings.ShopId = Newtonsoft.Json.JsonConvert.DeserializeObject<Shops>(content).results.First()
                        .shop_id;


                    //ayarları sakla
                    await _settingService.SaveSettingAsync(settings);
                    await _settingService.ClearCacheAsync();

                    _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

                    return await Configure();
                }
                else
                {
                    return await Configure();
                }
            }
            else
            {
                return await Configure();
            }
        }

        #endregion
    }
}