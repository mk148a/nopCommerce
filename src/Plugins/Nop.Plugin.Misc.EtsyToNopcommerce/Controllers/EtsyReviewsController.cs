using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core;
using Nop.Plugin.Misc.EtsyToNopcommerce.Models;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using System.Net;
using RestSharp;
using Nop.Plugin.Misc.EtsyToNopcommerce.Models.Shops;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Nop.Plugin.Misc.EtsyToNopcommerce.Models.Reviews;
using Nop.Plugin.Misc.EtsyToNopcommerce.Models.Listings;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Services.Catalog;
using Nop.Services.Customers;
using StackExchange.Profiling.Internal;
using Nop.Services.Directory;
using Nop.Data;
using Nop.Services.Common;
using StateProvince = Nop.Core.Domain.Directory.StateProvince;
using System.IO;
using DocumentFormat.OpenXml.Presentation;
using ImageProcessor;
using ImageProcessor.Plugins.WebP.Imaging.Formats;
using LinqToDB.Common;
using Nop.Plugin.Misc.EtsyToNopcommerce.Domains;
using Nop.Plugin.Misc.EtsyToNopcommerce.Services;
using Nop.Services.Media;
using Picture = Nop.Core.Domain.Media.Picture;
using Nop.Core.Domain.Catalog;
using Nop.Services.Seo;
using Nop.Web.Factories;
using Nop.Web.Models.Customer;


namespace Nop.Plugin.Misc.EtsyToNopcommerce.Controllers
{

    [Area(AreaNames.Admin)]
    public class EtsyReviewsController : BasePluginController
    {
        #region Fields

        private readonly ILocalizationService _localizationService;
        private readonly INotificationService _notificationService;
        private readonly IPermissionService _permissionService;
        private readonly ISettingService _settingService;
        private readonly IStoreContext _storeContext;
        private readonly ICustomerService _customerService;
        private readonly IProductService _productService;
        private readonly ICountryService _countryService;
        private readonly IAddressService _addressService;
        private readonly IRepository<StateProvince> _stateProvinceRepository;
        private readonly IRepository<Address> _addressRepository;
        private readonly IPictureService _pictureService;
        private readonly ICustomProductReviewMappingService _customProductReviewMappingService;
        private readonly CatalogSettings _catalogSettings;
        private readonly IBackgroundQueue _queue;
        private readonly IUrlRecordService _urlRecordService;
        private readonly ICustomerRegistrationService _customerRegistrationService;
        private readonly ICustomerModelFactory _customerModelFactory;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly CustomerSettings _customerSettings;
        private readonly IProductReviewsEtsyReviewService _etsyReviewService;
        private readonly IProductReviewsTransactionsMappingService _productReviewsTransactionsMappingService;
        #endregion

        #region Ctor

        public EtsyReviewsController(ILocalizationService localizationService,
            INotificationService notificationService,
            IPermissionService permissionService,
            ISettingService settingService,
            IStoreContext storeContext,
           ICustomerService customerService,IProductService productService,ICountryService countryService, IAddressService addressService,
            IRepository<StateProvince> stateProvinceRepository, IRepository<Address> addressRepository, IPictureService pictureService,
            ICustomProductReviewMappingService customProductReviewMappingService, CatalogSettings catalogSettings, IBackgroundQueue queue,
            IUrlRecordService urlRecordService, ICustomerRegistrationService customerRegistrationService, ICustomerModelFactory customerModelFactory,
            IGenericAttributeService genericAttributeService, CustomerSettings customerSettings, IProductReviewsEtsyReviewService etsyReviewService, IProductReviewsTransactionsMappingService productReviewsTransactionsMappingService)
        {
            _localizationService = localizationService;
            _notificationService = notificationService;
            _permissionService = permissionService;
            _settingService = settingService;
            _storeContext = storeContext;
            _productService= productService;
            _customerService = customerService;
            _countryService= countryService;
            _addressService= addressService;
            _stateProvinceRepository= stateProvinceRepository;
            _addressRepository= addressRepository;
            _pictureService= pictureService;
            _customProductReviewMappingService= customProductReviewMappingService;
            _catalogSettings= catalogSettings;
            _queue= queue;
            _urlRecordService = urlRecordService;
            _customerRegistrationService = customerRegistrationService;
            _customerModelFactory= customerModelFactory;
            _genericAttributeService= genericAttributeService;
            _customerSettings= customerSettings;
            _etsyReviewService=etsyReviewService;
            _productReviewsTransactionsMappingService=productReviewsTransactionsMappingService;
        }

        #endregion



        #region Methods
    
        [HttpPost]
        public async Task<IActionResult> InsertEtsyReviewsToNopcommerce(IEnumerable<EtsyReview> fromDelist)
        {
            var storeId = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var settings = await _settingService.LoadSettingAsync<EtsyToNopcommerceSettings>(storeId);

            if (settings != null)
            {
                _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));
                //Todo:buraya servisler ile istek ekle
                //  string reviewResult=  await ReviewlariAlVeIsle();

                //if (reviewResult != null)
                //{
                //      _notificationService.SuccessNotification(reviewResult.Split(",")[0] +" adet yorum, "+ reviewResult.Split(",")[1] +" adet fotograflı yorum eklendi");
                //  }

                
            return RedirectToRoute("Plugin.Misc.EtsyToNopcommerce.Configure");
            }
            else
            {
                return RedirectToRoute("Plugin.Misc.EtsyToNopcommerce.Configure");
            }
        }

     



        #endregion
    }
}