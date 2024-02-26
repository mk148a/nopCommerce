using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc;
using Nop.Core.Domain.Stores;
using Nop.Core.Infrastructure;
using Nop.Core;
using Nop.Plugin.Misc.GoogleShoppingMultiCountry.Domains;
using Nop.Plugin.Misc.GoogleShoppingMultiCountry.Models;
using Nop.Plugin.Misc.GoogleShoppingMultiCountry.Services;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Messages;
using Nop.Services.Plugins;
using Nop.Services.Security;
using Nop.Services.Stores;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;
using Nop.Web.Framework;
using Nop.Web.Framework.Models.Extensions;

namespace Nop.Plugin.Misc.GoogleShoppingMultiCountry.Controllers
{
    [AuthorizeAdmin]
    [Area(AreaNames.Admin)]
    public class FeedGoogleShoppingController : BasePluginController
    {
        #region Fields

        private readonly ICurrencyService _currencyService;
        private readonly ILanguageService _languageService;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly IGoogleService _googleService;
        private readonly ILocalizationService _localizationService;
        private readonly INopFileProvider _nopFileProvider;
        private readonly INotificationService _notificationService;
        private readonly ILogger _logger;
        private readonly IPermissionService _permissionService;
        private readonly IPluginService _pluginService;
        private readonly IProductService _productService;
        private readonly ISettingService _settingService;
        private readonly IStoreContext _storeContext;
        private readonly IStoreService _storeService;
        private readonly IWebHelper _webHelper;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IWorkContext _workContext;

        #endregion

        #region Ctor

        public FeedGoogleShoppingController(ICurrencyService currencyService,
            IGenericAttributeService genericAttributeService,
            IGoogleService googleService,
            ILocalizationService localizationService,
            INopFileProvider nopFileProvider,
            INotificationService notificationService,
            ILogger logger,
            IPermissionService permissionService,
            IPluginService pluginService,
            IProductService productService,
            ISettingService settingService,
            IStoreContext storeContext,
            IStoreService storeService,
            IWebHelper webHelper,
            IWebHostEnvironment webHostEnvironment,
            IWorkContext workContext,
            ILanguageService languageService)
        {
            _currencyService = currencyService;
            _genericAttributeService = genericAttributeService;
            _googleService = googleService;
            _localizationService = localizationService;
            _nopFileProvider = nopFileProvider;
            _notificationService = notificationService;
            _logger = logger;
            _permissionService = permissionService;
            _pluginService = pluginService;
            _productService = productService;
            _settingService = settingService;
            _storeContext = storeContext;
            _storeService = storeService;
            _webHelper = webHelper;
            _webHostEnvironment = webHostEnvironment;
            _workContext = workContext;
            _languageService = languageService;
        }

        #endregion

        #region Utilites

        /// <summary>
        /// Prepare FeedGoogleShoppingModel
        /// </summary>
        /// <param name="model">Model</param>
        private async Task PrepareModelAsync(FeedGoogleShoppingModel model)
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePlugins))
                return;

            //load settings for a chosen store scope
            var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var googleShoppingSettings = await _settingService.LoadSettingAsync<GoogleShoppingMultiCountrySettings>(storeScope);

            model.ProductPictureSize = googleShoppingSettings.ProductPictureSize;
            model.PassShippingInfoWeight = googleShoppingSettings.PassShippingInfoWeight;
            model.PassShippingInfoDimensions = googleShoppingSettings.PassShippingInfoDimensions;
            model.PricesConsiderPromotions = googleShoppingSettings.PricesConsiderPromotions;

            //currencies
            model.CurrencyId = googleShoppingSettings.CurrencyId;
            foreach (var c in await _currencyService.GetAllCurrenciesAsync())
                model.AvailableCurrencies.Add(new SelectListItem { Text = c.Name, Value = c.Id.ToString() });
            //Google categories
            model.DefaultGoogleCategory = googleShoppingSettings.DefaultGoogleCategory;
            model.AvailableGoogleCategories.Add(new SelectListItem { Text = "Select a category", Value = "" });
            foreach (var gc in await _googleService.GetTaxonomyListAsync())
                model.AvailableGoogleCategories.Add(new SelectListItem { Text = gc, Value = gc });

            var currentCustomer = await _workContext.GetCurrentCustomerAsync();
            model.HideGeneralBlock = await _genericAttributeService.GetAttributeAsync<bool>(currentCustomer, GoogleShoppingMultiCountryDefaults.HideGeneralBlock);
            model.HideProductSettingsBlock = await _genericAttributeService.GetAttributeAsync<bool>(currentCustomer, GoogleShoppingMultiCountryDefaults.HideProductSettingsBlock);

            
            //file paths
            foreach (var store in await _storeService.GetAllStoresAsync())
            {
                foreach (var language in await _languageService.GetAllLanguagesAsync(false, store.Id))
                {
                    var localFilePath = _nopFileProvider.Combine(_webHostEnvironment.WebRootPath, "files", "exportimport", store.Id + "-" +"-"+ googleShoppingSettings.StaticFileName);
                    if (_nopFileProvider.FileExists(localFilePath))
                        model.GeneratedFiles.Add(new GeneratedFileModel
                        {
                            StoreName = store.Name,
                            FileUrl = $"{_webHelper.GetStoreLocation(false)}files/exportimport/{store.Id}-{language.UniqueSeoCode}-{googleShoppingSettings.StaticFileName}"
                        });

                }
              
            }
            //prepare nested search models
            model.GoogleFeedProductSearchModel.SetGridPageSize();

            model.ActiveStoreScopeConfiguration = storeScope;
            if (storeScope > 0)
            {
                model.CurrencyId_OverrideForStore = await _settingService.SettingExistsAsync(googleShoppingSettings, x => x.CurrencyId, storeScope);
                model.DefaultGoogleCategory_OverrideForStore = await _settingService.SettingExistsAsync(googleShoppingSettings, x => x.DefaultGoogleCategory, storeScope);
                model.PassShippingInfoDimensions_OverrideForStore = await _settingService.SettingExistsAsync(googleShoppingSettings, x => x.PassShippingInfoDimensions, storeScope);
                model.PassShippingInfoWeight_OverrideForStore = await _settingService.SettingExistsAsync(googleShoppingSettings, x => x.PassShippingInfoWeight, storeScope);
                model.PricesConsiderPromotions_OverrideForStore = await _settingService.SettingExistsAsync(googleShoppingSettings, x => x.PricesConsiderPromotions, storeScope);
                model.ProductPictureSize_OverrideForStore = await _settingService.SettingExistsAsync(googleShoppingSettings, x => x.ProductPictureSize, storeScope);
            }
        }

        #endregion

        #region Methods

        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public async Task<IActionResult> Configure()
        {
            var model = new FeedGoogleShoppingModel();
            await PrepareModelAsync(model);

            return View("~/Plugins/Nop.Plugin.Misc.GoogleShoppingMultiCountry/Views/Configure.cshtml", model);
        }

        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        [HttpPost]
        [FormValueRequired("save")]
        [AutoValidateAntiforgeryToken]
        public async Task<IActionResult> Configure(FeedGoogleShoppingModel model)
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePlugins))
                return AccessDeniedView();

            if (!ModelState.IsValid)
            {
                return await Configure();
            }

            //load settings for a chosen store scope
            var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var googleShoppingSettings = await _settingService.LoadSettingAsync<GoogleShoppingMultiCountrySettings>(storeScope);

            //save settings
            googleShoppingSettings.ProductPictureSize = model.ProductPictureSize;
            googleShoppingSettings.PassShippingInfoWeight = model.PassShippingInfoWeight;
            googleShoppingSettings.PassShippingInfoDimensions = model.PassShippingInfoDimensions;
            googleShoppingSettings.PricesConsiderPromotions = model.PricesConsiderPromotions;
            googleShoppingSettings.CurrencyId = model.CurrencyId;
            googleShoppingSettings.DefaultGoogleCategory = model.DefaultGoogleCategory;

            //_settingService.SaveSetting(_googleShoppingSettings);

            /* We do not clear cache after each setting update.
             * This behavior can increase performance because cached settings will not be cleared 
             * and loaded from database after each update */
            await _settingService.SaveSettingOverridablePerStoreAsync(googleShoppingSettings, x => x.CurrencyId, model.CurrencyId_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(googleShoppingSettings, x => x.DefaultGoogleCategory, model.DefaultGoogleCategory_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(googleShoppingSettings, x => x.PassShippingInfoDimensions, model.PassShippingInfoDimensions_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(googleShoppingSettings, x => x.PassShippingInfoWeight, model.PassShippingInfoWeight_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(googleShoppingSettings, x => x.PricesConsiderPromotions, model.PricesConsiderPromotions_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(googleShoppingSettings, x => x.ProductPictureSize, model.ProductPictureSize_OverrideForStore, storeScope, false);

            //now clear settings cache
            await _settingService.ClearCacheAsync();

            _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

            //redisplay the form
            return await Configure();
        }

        [HttpPost, ActionName("Configure")]
        [FormValueRequired("generate")]
        [AutoValidateAntiforgeryToken]
        public async Task<IActionResult> GenerateFeed(FeedGoogleShoppingModel model)
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePlugins))
                return AccessDeniedView();

            //load settings for a chosen store scope
            var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();

            try
            {
                //plugin
                var pluginDescriptor = await _pluginService.GetPluginDescriptorBySystemNameAsync<IPlugin>("Nop.Plugin.Misc.GoogleShoppingMultiCountry");
                if (pluginDescriptor == null || pluginDescriptor.Instance<IPlugin>() is not GoogleShoppingMultiCountry plugin)
                    throw new Exception(await _localizationService.GetResourceAsync("Plugins.Feed.GoogleShopping.ExceptionLoadPlugin"));

                var stores = new List<Store>();
                var storeById = await _storeService.GetStoreByIdAsync(storeScope);
                if (storeScope > 0)
                    stores.Add(storeById);
                else
                    stores.AddRange(await _storeService.GetAllStoresAsync());

                foreach (var store in stores)
                    await plugin.GenerateStaticFileAsync(store);

                _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Plugins.Feed.GoogleShopping.SuccessResult"));
            }
            catch (Exception exc)
            {
                _notificationService.ErrorNotification(exc.Message);
                await _logger.ErrorAsync(exc.Message, exc);
            }

            return await Configure();
        }

        [HttpPost]
        [AutoValidateAntiforgeryToken]
        public async Task<IActionResult> GoogleProductList(GoogleFeedProductSearchModel searchModel)
        {
            var storeId = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var products = await _productService.SearchProductsAsync(
                storeId: storeId,
                pageIndex: searchModel.Page - 1,
                pageSize: searchModel.PageSize,
                showHidden: true);

            //prepare list model
            var model = await new GoogleFeedProductListModel().PrepareToGridAsync(searchModel, products, () =>
            {
                return products.SelectAwait(async product =>
                {
                    var gModel = new GoogleFeedProductModel
                    {
                        ProductId = product.Id,
                        ProductName = product.Name
                    };
                    var googleProduct = await _googleService.GetByProductIdAsync(product.Id);
                    if (googleProduct != null)
                    {
                        gModel.GoogleCategory = googleProduct.Taxonomy;
                        gModel.Gender = googleProduct.Gender;
                        gModel.AgeGroup = googleProduct.AgeGroup;
                        gModel.Color = googleProduct.Color;
                        gModel.GoogleSize = googleProduct.Size;
                        gModel.CustomGoods = googleProduct.CustomGoods;
                        gModel.LanguageId=googleProduct.LanguageId;
                    }
                    return gModel;
                });
            });

            return Json(model);
        }

        public async Task<IActionResult> Edit(int id)
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManageShippingSettings))
                return AccessDeniedView();

            var googleProduct = await _googleService.GetByProductIdAsync(id);

            var model = new GoogleFeedProductModel
            {
                ProductId = id
            };

            if (googleProduct == null)
                return View("~/Plugins/Nop.Plugin.Misc.GoogleShoppingMultiCountry/Views/Edit.cshtml", model);

            model = new GoogleFeedProductModel
            {
                Id = googleProduct.Id,
                ProductId = googleProduct.ProductId,
                Color = googleProduct.Color,
                AgeGroup = googleProduct.AgeGroup,
                CustomGoods = googleProduct.CustomGoods,
                Gender = googleProduct.Gender,
                GoogleSize = googleProduct.Size,
                GoogleCategory = googleProduct.Taxonomy,
                LanguageId = googleProduct.LanguageId
            };

            return View("~/Plugins/Nop.Plugin.Misc.GoogleShoppingMultiCountry/Views/Edit.cshtml", model);
        }

        [HttpPost]
        [AutoValidateAntiforgeryToken]
        public async Task<IActionResult> Edit(GoogleFeedProductModel model)
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePlugins))
                return AccessDeniedView();

            var googleProduct = await _googleService.GetByProductIdAsync(model.ProductId);
            if (googleProduct != null)
            {
                googleProduct.Taxonomy = model.GoogleCategory;
                googleProduct.Gender = model.Gender;
                googleProduct.AgeGroup = model.AgeGroup;
                googleProduct.Color = model.Color;
                googleProduct.Size = model.GoogleSize;
                googleProduct.CustomGoods = model.CustomGoods;
                googleProduct.LanguageId = model.LanguageId;
                await _googleService.UpdateGoogleProductRecordAsync(googleProduct);
            }
            else
            {
                //insert
                googleProduct = new GoogleFeedProductRecord
                {
                    ProductId = model.ProductId,
                    Taxonomy = model.GoogleCategory,
                    Gender = model.Gender,
                    AgeGroup = model.AgeGroup,
                    Color = model.Color,
                    Size = model.GoogleSize,
                    CustomGoods = model.CustomGoods,
                    LanguageId = model.LanguageId
                };
                await _googleService.InsertGoogleProductRecordAsync(googleProduct);
            }

            ViewBag.RefreshPage = true;

            return View("~/Plugins/Nop.Plugin.Misc.GoogleShoppingMultiCountry/Views/Edit.cshtml", model);
        }

        #endregion
    }
}
