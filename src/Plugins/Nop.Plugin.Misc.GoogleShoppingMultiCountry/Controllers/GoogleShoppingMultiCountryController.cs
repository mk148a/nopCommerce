using System;
using System.Collections.Generic;
using System.Linq;

using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
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
using JsonResult = Microsoft.AspNetCore.Mvc.JsonResult;
using SelectListItem = Microsoft.AspNetCore.Mvc.Rendering.SelectListItem;


namespace Nop.Plugin.Misc.GoogleShoppingMultiCountry.Controllers
{
    [AuthorizeAdmin]
    [Area(AreaNames.ADMIN)]
    public class GoogleShoppingMultiCountryController : BasePluginController
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
        private readonly ICategoryService _categoryService;

        #endregion

        #region Ctor

        public GoogleShoppingMultiCountryController(ICurrencyService currencyService,
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
            ILanguageService languageService,
          ICategoryService categoryService)
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
            _categoryService = categoryService;
        }

        #endregion

        #region Utilites

        /// <summary>
        /// Prepare FeedGoogleShoppingModel
        /// </summary>
        /// <param name="model">Model</param>
        [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
        private async Task<GoogleShoppingMultiCountryModel> PrepareModelAsync(GoogleShoppingMultiCountryModel model)
        {
           

            //load settings for a chosen store scope
            var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var googleShoppingSettings = await _settingService.LoadSettingAsync<GoogleShoppingMultiCountrySettings>(storeScope);

            model.ProductPictureSize = googleShoppingSettings.ProductPictureSize;
            model.PassShippingInfoWeight = googleShoppingSettings.PassShippingInfoWeight;
            model.PassShippingInfoDimensions = googleShoppingSettings.PassShippingInfoDimensions;
            model.PricesConsiderPromotions = googleShoppingSettings.PricesConsiderPromotions;


            //Google categories
            await _googleService.CreateTaxonomyEntityAsync();

            model.DefaultGoogleCategory = googleShoppingSettings.DefaultGoogleCategory;
            model.DefaultGoogleCategoryId = googleShoppingSettings.DefaultGoogleCategory.Split(";").First();
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
                    var localFilePath = _nopFileProvider.Combine(_webHostEnvironment.WebRootPath, "files", "exportimport", store.Id + "-" + language.UniqueSeoCode+"-" + googleShoppingSettings.StaticFileName);
                    if (_nopFileProvider.FileExists(localFilePath))
                        model.GeneratedFiles.Add(new GeneratedFileModel
                        {
                            StoreName = store.Name,
                            FileUrl = $"{_webHelper.GetStoreLocation(false)}files/exportimport/{store.Id}-{language.UniqueSeoCode}-{googleShoppingSettings.StaticFileName}",
                            Language = language.Name,
                        });

                }
              
            }
            //prepare nested search models
            model.GoogleFeedProductSearchModel.SetGridPageSize();

            model.ActiveStoreScopeConfiguration = storeScope;
            if (storeScope > 0)
            {
                model.DefaultGoogleCategory_OverrideForStore = await _settingService.SettingExistsAsync(googleShoppingSettings, x => x.DefaultGoogleCategory, storeScope);
                model.DefaultGoogleCategoryId_OverrideForStore = await _settingService.SettingExistsAsync(googleShoppingSettings, x => x.DefaultGoogleCategoryId, storeScope);
                model.PassShippingInfoDimensions_OverrideForStore = await _settingService.SettingExistsAsync(googleShoppingSettings, x => x.PassShippingInfoDimensions, storeScope);
                model.PassShippingInfoWeight_OverrideForStore = await _settingService.SettingExistsAsync(googleShoppingSettings, x => x.PassShippingInfoWeight, storeScope);
                model.PricesConsiderPromotions_OverrideForStore = await _settingService.SettingExistsAsync(googleShoppingSettings, x => x.PricesConsiderPromotions, storeScope);
                model.ProductPictureSize_OverrideForStore = await _settingService.SettingExistsAsync(googleShoppingSettings, x => x.ProductPictureSize, storeScope);
            }

            return model;
        }

        #endregion

        #region Methods

        [AuthorizeAdmin]
        [Area(AreaNames.ADMIN)]
        public async Task<IActionResult> Configure()
        {
            var model = new GoogleShoppingMultiCountryModel();
            try
            {
                model = await PrepareModelAsync(model);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
               
            }
          

            return View("~/Plugins/Nop.Plugin.Misc.GoogleShoppingMultiCountry/Views/Configure.cshtml", model);
        }

        [AuthorizeAdmin]
        [Area(AreaNames.ADMIN)]
        [Microsoft.AspNetCore.Mvc.HttpPost]
        [FormValueRequired("save")]
        [AutoValidateAntiforgeryToken]
        [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
        public async Task<IActionResult> Configure(GoogleShoppingMultiCountryModel model)
        {
          

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
            googleShoppingSettings.DefaultGoogleCategory = model.DefaultGoogleCategory;
            googleShoppingSettings.DefaultGoogleCategoryId = model.DefaultGoogleCategoryId;

            //_settingService.SaveSetting(_googleShoppingSettings);

            /* We do not clear cache after each setting update.
             * This behavior can increase performance because cached settings will not be cleared 
             * and loaded from database after each update */
            await _settingService.SaveSettingOverridablePerStoreAsync(googleShoppingSettings, x => x.DefaultGoogleCategory, model.DefaultGoogleCategory_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(googleShoppingSettings, x => x.DefaultGoogleCategoryId, model.DefaultGoogleCategoryId_OverrideForStore, storeScope, false);
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

        [Microsoft.AspNetCore.Mvc.HttpPost, Microsoft.AspNetCore.Mvc.ActionName("Configure")]
        [FormValueRequired("generate")]
        [AutoValidateAntiforgeryToken]
        [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
        public async Task<IActionResult> GenerateFeed(GoogleShoppingMultiCountryModel model)
        {
         

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

            return await Configure(model);
        }

        [Microsoft.AspNetCore.Mvc.HttpPost]
        [AutoValidateAntiforgeryToken]
        [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
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

        [CheckPermission(StandardPermission.Configuration.MANAGE_SHIPPING_SETTINGS)]
        public async Task<IActionResult> Edit(int id)
        {
           

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

        [Microsoft.AspNetCore.Mvc.HttpPost]
        [AutoValidateAntiforgeryToken]
        [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
        public async Task<IActionResult> Edit(GoogleFeedProductModel model)
        {
          

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



        #region CategoriesGoogleCategoriesMapping

        [Microsoft.AspNetCore.Mvc.HttpPost]
        [AutoValidateAntiforgeryToken]
        public async Task<IActionResult> GoogleCategoryList(GoogleFeedCategorySearchModel searchModel)
        {
            var storeId = await _storeContext.GetActiveStoreScopeConfigurationAsync();
          
          
            var categories = await _categoryService.GetAllCategoriesAsync("",storeId:storeId,
                pageIndex: searchModel.Page - 1,
                pageSize: searchModel.PageSize,
                showHidden: false);
            //prepare list model
            var model = await new GoogleFeedCategoryListModel().PrepareToGridAsync(searchModel, categories, () =>
            {
                return categories.SelectAwait(async category =>
                {
                    var gModel = new GoogleFeedCategoryModel
                    {
                        CategoryId = category.Id,
                        CategoryName = category.Name
                    };
                    var googleCategory = await _googleService.GetByCategoryIdAsync(category.Id);
                    if (googleCategory != null)
                    {
                        gModel.GoogleCategory = googleCategory.Name;
                        gModel.GoogleCategoryId = googleCategory.GoogleTaxonomyId;
                       
                    }
                    return gModel;
                });
            });

            return Json(model);
        }

        [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
        private async Task<GoogleFeedCategoryMappingModel> PrepareCategoryMappingModelAsync(GoogleFeedCategoryMappingModel model)
        {
           

            //load settings for a chosen store scope
            var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var googleShoppingSettings = await _settingService.LoadSettingAsync<GoogleShoppingMultiCountrySettings>(storeScope);

            //Google and Nopcommerce categories
            var googleCategories=   await _googleService.GetTaxonomyListEntityAsync();
            var categories = await _categoryService.GetAllCategoriesAsync(storeScope);
            var categoryTaxonomyMappings = await _googleService.GetGoogleTaxonomyRecordMappingsAsync();
            model.Categories= categories;

            model.CategoryGoogleTaxonomyRecordMappings = categoryTaxonomyMappings;
            model.GoogleFeedCategoryListSearchModel.SetGridPageSize(model.GoogleFeedCategoryListSearchModel.PageSize);



        
        

            

            return model;
        }

        [Microsoft.AspNetCore.Mvc.HttpPost, Microsoft.AspNetCore.Mvc.ActionName("SearchGoogleTaxonomyPrefixAsync")]
        [AutoValidateAntiforgeryToken]
        public async Task<JsonResult> SearchGoogleTaxonomyPrefixAsync(string Prefix)
        {
            //Note : you can bind same list from database
            var taxonomyList=await _googleService.GetTaxonomyListEntityAsync();
            //Searching records from list using LINQ query
            var Taxonomies = (from N in taxonomyList
                        where N.Name.ToLower().Contains(Prefix.ToLower())
                              select new { N.Name, N.GoogleTaxonomyId, parentTaxonomy =  _googleService.GetFullTaxonomyNameByTaxonomyId(N.GoogleTaxonomyId) });

            return Json(Taxonomies, new Newtonsoft.Json.JsonSerializerSettings());
        }


        [Microsoft.AspNetCore.Mvc.HttpGet, Microsoft.AspNetCore.Mvc.ActionName("MapCategories")]
        [AutoValidateAntiforgeryToken]
        public async Task<IActionResult> MapCategories()
        {
            var model = new GoogleFeedCategoryMappingModel();
            try
            {
               
                model = await PrepareCategoryMappingModelAsync(model);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);

            }


            return View("~/Plugins/Nop.Plugin.Misc.GoogleShoppingMultiCountry/Views/_MapCategories.cshtml", model);
        }



        [CheckPermission(StandardPermission.Configuration.MANAGE_SHIPPING_SETTINGS)]
        public async Task<IActionResult> EditMapCategories(int id)
        {

            var googleCategoryMapping = await _googleService.GetGoogleTaxonomyRecordMappingByCategoryIdAsync(id);
            var googleCategory = await _googleService.GetByCategoryIdAsync(id);
            var category = await _categoryService.GetCategoryByIdAsync(id);

            var model = new GoogleFeedCategoryModel
            {
                CategoryId = id,
              
            };
            if (category!=null)
            {
                model.CategoryName = category.Name;
            }

            if (googleCategory == null)
                return View("~/Plugins/Nop.Plugin.Misc.GoogleShoppingMultiCountry/Views/EditMapCategories.cshtml", model);

            if (category != null)
            {
                model = new GoogleFeedCategoryModel
                {
                    Id = googleCategory.Id,
                    CategoryId = googleCategoryMapping.CategoryId,
                    CategoryName = category.Name,
                    GoogleCategory = googleCategory.Name,
                    GoogleCategoryId = googleCategory.Id
                };
            }

            return View("~/Plugins/Nop.Plugin.Misc.GoogleShoppingMultiCountry/Views/EditMapCategories.cshtml", model);
        }

        [Microsoft.AspNetCore.Mvc.HttpPost]
        [AutoValidateAntiforgeryToken]
        [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
        public async Task<IActionResult> EditMapCategories(GoogleFeedCategoryModel model)
        {
           

            var categoryGoogleTaxonomyRecordMapping = await _googleService.GetGoogleTaxonomyRecordMappingByCategoryIdAsync(model.CategoryId);

            if (categoryGoogleTaxonomyRecordMapping != null)
            {
                categoryGoogleTaxonomyRecordMapping.GoogleTaxonomyRecordId = model.GoogleCategoryId;
                categoryGoogleTaxonomyRecordMapping.CategoryId = model.CategoryId;
                await _googleService.UpdateCategoryGoogleTaxonomyRecordMappingAsync(categoryGoogleTaxonomyRecordMapping);
            }
            else
            {
                //insert
                categoryGoogleTaxonomyRecordMapping = new CategoryGoogleTaxonomyRecordMapping
                {
                    CategoryId = model.CategoryId,
                    GoogleTaxonomyRecordId = model.GoogleCategoryId
                };
                await _googleService.InsertCategoryGoogleTaxonomyRecordMappingAsync(categoryGoogleTaxonomyRecordMapping);
            }

            ViewBag.RefreshPage = true;

            return View("~/Plugins/Nop.Plugin.Misc.GoogleShoppingMultiCountry/Views/EditMapCategories.cshtml", model);
        }

        #endregion

        #endregion
    }
}
