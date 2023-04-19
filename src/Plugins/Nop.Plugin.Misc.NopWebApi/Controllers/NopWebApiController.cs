using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Localization;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Security;
using Nop.Core.Domain.Shipping;
using Nop.Core.Events;
using Nop.Core.Infrastructure;
using Nop.Core;
using Nop.Services.Catalog;
using Nop.Services.Customers;
using Nop.Services.Html;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Media;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Security;
using Nop.Services.Seo;
using Nop.Services.Stores;
using Nop.Web.Framework.Controllers;
using Nop.Core.Domain.Customers;
using Nop.Plugin.Misc.NopWebApi.Models;
using Nop.Data;
using Nop.Web.Areas.Admin.Factories;
using Nop.Web.Areas.Admin.Models.Catalog;
using Nop.Web.Areas.Admin.Models.Customers;
using static Nop.Plugin.Misc.NopWebApi.Models.Varyasyonlar;

namespace Nop.Plugin.Misc.NopWebApi.Controllers
{
    [AutoValidateAntiforgeryToken]
    public class NopWebApiController:BasePluginController
    {

        #region Fields

       
        private readonly ILocalizationService _localizationService;
        private readonly IOrderService _orderService;
        private readonly IProductAttributeParser _productAttributeParser;
        private readonly IProductModelFactory _productModelFactory;
        private readonly IProductService _productService;
        private readonly IStoreContext _storeContext;
        private readonly IWebHelper _webHelper;
        private readonly IWorkContext _workContext;
        private readonly IWorkflowMessageService _workflowMessageService;
        private readonly LocalizationSettings _localizationSettings;
        private readonly ShippingSettings _shippingSettings;
        private readonly IPictureService _pictureService;
        private readonly INopFileProvider _fileProvider;
        private readonly ICustomerRegistrationService _customerRegistrationService;
        protected readonly IRepository<Product> _productRepository;
        private readonly IProductAttributeService _productAttributeService;
        private readonly ICustomerService _customerService;
        private readonly ICustomerRoleModelFactory _customerRoleModelFactory;
        


        #endregion

        #region Ctor

        public NopWebApiController(CaptchaSettings captchaSettings,
            CatalogSettings catalogSettings,
            IAclService aclService,
            ICompareProductsService compareProductsService,
            ICustomerActivityService customerActivityService,
            ICustomerService customerService,
            IEventPublisher eventPublisher,
            IHtmlFormatter htmlFormatter,
            ILocalizationService localizationService,
            IOrderService orderService,
            IPermissionService permissionService,
            IProductAttributeParser productAttributeParser,
            Web.Areas.Admin.Factories.IProductModelFactory productModelFactory,
            IProductService productService,
            IRecentlyViewedProductsService recentlyViewedProductsService,
            IReviewTypeService reviewTypeService,
            IShoppingCartModelFactory shoppingCartModelFactory,
            IShoppingCartService shoppingCartService,
            IStoreContext storeContext,
            IStoreMappingService storeMappingService,
            IUrlRecordService urlRecordService,
            IWebHelper webHelper,
            IWorkContext workContext,
            IWorkflowMessageService workflowMessageService,
            LocalizationSettings localizationSettings,
            ShoppingCartSettings shoppingCartSettings,
            IPictureService pictureService,
            INopFileProvider fileProvider,
            ShippingSettings shippingSettings,
            ICustomerRegistrationService customerRegistrationService,
             IRepository<Product> productRepository,
            IProductAttributeService iAttributeService,
            ICustomerRoleModelFactory customerRoleModelFactory
            

            

        )
        {
            _pictureService = pictureService;
            _fileProvider = fileProvider;
            _localizationService = localizationService;
            _orderService = orderService;
            _productAttributeParser = productAttributeParser;
            _productModelFactory = productModelFactory;
            _productService = productService;
            _storeContext = storeContext;
            _webHelper = webHelper;
            _workContext = workContext;
            _workflowMessageService = workflowMessageService;
            _localizationSettings = localizationSettings;
            _shippingSettings = shippingSettings;
            _customerRegistrationService=customerRegistrationService;
            _productRepository = productRepository;
            _productAttributeParser = productAttributeParser;
            _productModelFactory = productModelFactory;
            _productService = productService;
            _customerRegistrationService = customerRegistrationService;
            _shippingSettings = shippingSettings;
            _customerRegistrationService = customerRegistrationService;
            _productAttributeService = iAttributeService;
            _customerService=customerService;
            _customerRoleModelFactory=customerRoleModelFactory;




        }


        #endregion

        #region Methods

        //public async Task<IActionResult> Configure()
        //{
        //    //if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManageShippingSettings))
        //    //    return AccessDeniedView();

        //    ////prepare model
        //    //var model = await _storePickupPointModelFactory.PrepareStorePickupPointSearchModelAsync(new StorePickupPointSearchModel());

        //    return View("~/Plugins/Pickup.PickupInStore/Views/Configure.cshtml", model);
        //}


        //[FormValueRequired("add-review")]
       
        [RequestFormLimits(MultipartBodyLengthLimit = 1048576000)]
        [RequestSizeLimit(1048576000)]
        public virtual async Task<IActionResult> GetAllProductsAsync(string name,string passs)
        {
            var loginResult = await _customerRegistrationService.ValidateCustomerAsync(name, passs);

            List<Urunler> urunler = new List<Urunler>();
            List<Varyasyonlar> varyasyonlar = new List<Varyasyonlar>();
            ProductResult sonuc = new ProductResult();

            switch (loginResult)
            {
                case CustomerLoginResults.Successful:
                {
                    var customer = await
                        _customerService.GetCustomerByEmailAsync(name);

                    var customerRole = await
                        _customerService.GetCustomerRolesAsync(customer);
                      

                        if (customerRole.Any(x=>x.Name.ToLower().Contains("admin")))
                        {
                            ProductSearchModel sModel = new ProductSearchModel();
                            sModel.Length = 10000;

                            var productList = await _productModelFactory.PrepareProductListModelAsync(sModel);

                            var proRep = _productRepository.GetAll();

                            foreach (var siteProduct in productList.Data)
                            {
                                Urunler yeni = new Urunler();

                                yeni.Sku = siteProduct.Sku;
                                yeni.SatisFiyati = siteProduct.Price;
                                yeni.UrunAdi = siteProduct.Name;

                                var productPicture = await _pictureService.GetPicturesByProductIdAsync(siteProduct.Id);

                              
                                if (productPicture.Count > 0)
                                {
                                    var pictureId = (await _pictureService.GetPictureByIdAsync(productPicture.First().Id))
                                                    ?? throw new Exception("Picture cannot be loaded");

                                    yeni.FotografLink = (await _pictureService.GetPictureUrlAsync(pictureId)).Url;


                                }

                               
                                    var pr = proRep.Where(x => x.Id == siteProduct.Id);

                                    if (pr != null)
                                    {

                                        var productAttributeMappinglist = await _productAttributeService.GetProductAttributeMappingsByProductIdAsync(siteProduct.Id);

                                        foreach (var productAttributeMapping in productAttributeMappinglist)
                                        {
                                            var productAttributeId = productAttributeMapping.ProductAttributeId;

                                            var productAttribute = await _productAttributeService.GetProductAttributeByIdAsync(
                                                    productAttributeId);

                                            var productAttributeValues =
                                                await _productAttributeService.GetProductAttributeValuesAsync(
                                                    productAttributeMapping.Id);

                                            if (productAttributeValues.Count > 0)
                                            {

                                                foreach (var productAttributeValue in productAttributeValues)
                                                {
                                                    Varyasyonlar yeniVaryasyonlar = new Varyasyonlar();

                                                    string varyasyonTuru = productAttribute.Name;
                                                    string varyasyonDegeri = productAttributeValue.Name;

                                                    if (productAttributeValue.PictureId != 0)
                                                    {
                                                        try
                                                        {
                                                            var picture = await _pictureService.GetPictureByIdAsync(productAttributeValue.PictureId);

                                                            if (picture != null)
                                                            {
                                                                string url = await _pictureService.GetPictureUrlAsync(picture.Id);

                                                                yeniVaryasyonlar.FotografLinki = url;
                                                        }
                                                            

                                                        }
                                                        catch
                                                        {

                                                            yeniVaryasyonlar.FotografLinki = null;
                                                        }
                                                      

                                                    }

                                                if (productAttributeValue.PriceAdjustment > 0)
                                                {
                                                    yeniVaryasyonlar.SatisFiyati = siteProduct.Price + productAttributeValue.PriceAdjustment;
                                                }
                                                yeniVaryasyonlar.NopCommerceVaryasyonValueId = productAttributeValue.Id;
                                                
                                                
                                                yeniVaryasyonlar.VaryasyonTuru = productAttribute.Name.ToLower();
                                            yeniVaryasyonlar.VaryasyonAdi = varyasyonDegeri.ToLower();
                                            yeniVaryasyonlar.NopCommerceVaryasyonId = productAttributeId;

                                                if (varyasyonTuru.Contains("pcs"))
                                                {
                                                    yeniVaryasyonlar.Tip =
                                                        VaryasyonTipleri.Adet;
                                                    try
                                                    {
                                                        if (varyasyonDegeri
                                                         .Contains("broadhead"))
                                                        {
                                                            yeniVaryasyonlar.Adet =
                                                                int.Parse(
                                                                    varyasyonDegeri
                                                                        .Replace(
                                                                            "pcs",
                                                                            "")
                                                                        .Replace(
                                                                            "with",
                                                                            "")
                                                                        .Replace(
                                                                            "broadhead",
                                                                            "")
                                                                        .Replace(
                                                                            " ",
                                                                            ""));

                                                        }
                                                        else
                                                        {
                                                            yeniVaryasyonlar.Adet =
                                                                int.Parse(
                                                                    varyasyonDegeri
                                                                        .Replace(
                                                                            "pcs",
                                                                            ""));

                                                        }

                                                    }
                                                    catch (Exception e)
                                                    {
                                                        Console.WriteLine(e);
                                                    }

                                                }
                                                else if (varyasyonTuru.Contains(
                                                     "color") ||
                                                 varyasyonTuru.Contains(
                                                     "colour"))
                                                {
                                                    yeniVaryasyonlar.Tip =
                                                        VaryasyonTipleri.Renk;
                                                }
                                                else if (varyasyonTuru.Contains(
                                                    "set"))
                                                {
                                                    yeniVaryasyonlar.Tip =
                                                        VaryasyonTipleri.Set;

                                                }
                                                else if (varyasyonTuru.Contains(
                                                    "length"))
                                                {
                                                    yeniVaryasyonlar.Tip =
                                                        VaryasyonTipleri.Boyut;
                                                }
                                                else if (varyasyonTuru.Contains(
                                                     "weight") ||
                                                 varyasyonTuru.Contains(
                                                     "size"))
                                                {
                                                    yeniVaryasyonlar.Tip =
                                                        VaryasyonTipleri.Boyut;
                                                }
                                                else
                                                {
                                                    yeniVaryasyonlar.Tip =
                                                        VaryasyonTipleri
                                                            .Özellik;
                                                }
                                                varyasyonlar.Add(yeniVaryasyonlar);



                                            }










                                        }




                                            }





                                           
                                        

                                    }

                                





                                urunler.Add(yeni);
                            }

                            sonuc.urunlerList = urunler;
                            sonuc.VaryasyonlarList = varyasyonlar;
                            sonuc.IsError = false;



                            return Json(sonuc);

                        }
                        else
                        {
                            ModelState.AddModelError("", await _localizationService.GetResourceAsync("Account.Login.WrongCredentials.LockedOut"));
                            return Json(await _localizationService.GetResourceAsync("Account.Login.WrongCredentials.LockedOut"));
                        }

                }
                case CustomerLoginResults.CustomerNotExist:
                    ModelState.AddModelError("", await _localizationService.GetResourceAsync("Account.Login.WrongCredentials.CustomerNotExist"));

                    return Json(await _localizationService.GetResourceAsync("Account.Login.WrongCredentials.CustomerNotExist"));
                   
                case CustomerLoginResults.Deleted:
                    ModelState.AddModelError("", await _localizationService.GetResourceAsync("Account.Login.WrongCredentials.Deleted"));
                    return Json(await _localizationService.GetResourceAsync("Account.Login.WrongCredentials.Deleted"));
                    
                case CustomerLoginResults.NotActive:
                    ModelState.AddModelError("", await _localizationService.GetResourceAsync("Account.Login.WrongCredentials.NotActive"));
                    return Json(await _localizationService.GetResourceAsync("Account.Login.WrongCredentials.NotActive"));
                case CustomerLoginResults.NotRegistered:
                    ModelState.AddModelError("", await _localizationService.GetResourceAsync("Account.Login.WrongCredentials.NotRegistered"));
                    return Json(await _localizationService.GetResourceAsync("Account.Login.WrongCredentials.NotRegistered"));
                case CustomerLoginResults.LockedOut:
                    ModelState.AddModelError("", await _localizationService.GetResourceAsync("Account.Login.WrongCredentials.LockedOut"));
                    return Json(await _localizationService.GetResourceAsync("Account.Login.WrongCredentials.LockedOut"));
                case CustomerLoginResults.WrongPassword:
                default:
                    ModelState.AddModelError("", await _localizationService.GetResourceAsync("Account.Login.WrongCredentials"));
                    return Json(await _localizationService.GetResourceAsync("Account.Login.WrongCredentials"));
            }
        







        }

        [RequestFormLimits(MultipartBodyLengthLimit = 1048576000)]
        [RequestSizeLimit(1048576000)]
        public virtual async Task<IActionResult> GetAllOrdersAsync(string name, string passs)
        {
            var loginResult = await _customerRegistrationService.ValidateCustomerAsync(name, passs);

            List<Urunler> urunler = new List<Urunler>();

            OrderResult sonuc = new OrderResult();

            switch (loginResult)
            {
                case CustomerLoginResults.Successful:
                    {
                        var customer = await
                            _customerService.GetCustomerByEmailAsync(name);

                        var customerRole = await
                            _customerService.GetCustomerRolesAsync(customer);


                        if (customerRole.Any(x => x.Name.ToLower().Contains("admin")))
                        {
                           var allOrders= await _orderService.SearchOrdersAsync();
                           var workingOrders = allOrders.Where(x => x.OrderStatus == OrderStatus.Complete|| x.OrderStatus == OrderStatus.Processing).ToList();
                            // workingOrders = workingOrders.Where(y => y.Deleted = false);

                            ProductSearchModel sModel = new ProductSearchModel();
                            sModel.Length = 10000;
                            var productList = await _productModelFactory.PrepareProductListModelAsync(sModel);
                            List<Siparisler> siparisList= new List<Siparisler>();
                            int i = 0;

                            for (int j = 0; j < workingOrders.Count(); j++)
                            {
                                var order = workingOrders[j];
                                DateTime siparisZamani = DateTime.Now;
                                if (order.PaidDateUtc != null)
                                {
                                    siparisZamani = order.PaidDateUtc.Value.AddHours(3);
                                }

                                var siparisurunleri = await _orderService.GetOrderItemsAsync(order.Id);

                                foreach (var siparisUrunu in siparisurunleri)
                                {

                                    
                                    var sku = productList.Data.Where(x => x.Id == siparisUrunu.ProductId);
                                    if (sku.Any())
                                    {


                                        var siparis = new Siparisler();
                                        siparis.Adet = siparisUrunu.Quantity;
                                        siparis.BirimFiyat = siparisUrunu.UnitPriceExclTax;
                                        siparis.ParaBirimi = order.CustomerCurrencyCode;
                                        siparis.ReceiptId = order.Id;
                                        siparis.SiparisDurumu = order.OrderStatus.ToString();
                                        siparis.SiparisTarih = siparisZamani;
                                        siparis.Tarih = DateTime.Now;
                                        siparis.TransacationId = order.Id;
                                        siparis.Sku = sku.FirstOrDefault().Sku;
                                        siparisList.Add(siparis);
                                    }






                                }
                            }
                       
                           sonuc.SiparisList= siparisList;






                            return Json(sonuc);

                        }
                        else
                        {
                            ModelState.AddModelError("", await _localizationService.GetResourceAsync("Account.Login.WrongCredentials.LockedOut"));
                            return Json(await _localizationService.GetResourceAsync("Account.Login.WrongCredentials.LockedOut"));
                        }

                    }
                case CustomerLoginResults.CustomerNotExist:
                    ModelState.AddModelError("", await _localizationService.GetResourceAsync("Account.Login.WrongCredentials.CustomerNotExist"));

                    return Json(await _localizationService.GetResourceAsync("Account.Login.WrongCredentials.CustomerNotExist"));

                case CustomerLoginResults.Deleted:
                    ModelState.AddModelError("", await _localizationService.GetResourceAsync("Account.Login.WrongCredentials.Deleted"));
                    return Json(await _localizationService.GetResourceAsync("Account.Login.WrongCredentials.Deleted"));

                case CustomerLoginResults.NotActive:
                    ModelState.AddModelError("", await _localizationService.GetResourceAsync("Account.Login.WrongCredentials.NotActive"));
                    return Json(await _localizationService.GetResourceAsync("Account.Login.WrongCredentials.NotActive"));
                case CustomerLoginResults.NotRegistered:
                    ModelState.AddModelError("", await _localizationService.GetResourceAsync("Account.Login.WrongCredentials.NotRegistered"));
                    return Json(await _localizationService.GetResourceAsync("Account.Login.WrongCredentials.NotRegistered"));
                case CustomerLoginResults.LockedOut:
                    ModelState.AddModelError("", await _localizationService.GetResourceAsync("Account.Login.WrongCredentials.LockedOut"));
                    return Json(await _localizationService.GetResourceAsync("Account.Login.WrongCredentials.LockedOut"));
                case CustomerLoginResults.WrongPassword:
                default:
                    ModelState.AddModelError("", await _localizationService.GetResourceAsync("Account.Login.WrongCredentials"));
                    return Json(await _localizationService.GetResourceAsync("Account.Login.WrongCredentials"));
            }








        }






        #endregion

    }
}
