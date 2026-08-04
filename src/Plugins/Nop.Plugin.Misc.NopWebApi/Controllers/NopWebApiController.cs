using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using LinqToDB.Common;
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
using Paragraph = DocumentFormat.OpenXml.Drawing.Paragraph;
using DocumentFormat.OpenXml.Math;
using System.Text.RegularExpressions;
using Nop.Core.Domain.Directory;
using Nop.Services.Directory;

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
        private readonly IHtmlFormatter _htmlFormatter;
        private readonly ICurrencyService _currencyService;



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
            ICustomerRoleModelFactory customerRoleModelFactory,
            ICurrencyService currencyService
            




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
            _htmlFormatter=htmlFormatter;
            _currencyService=currencyService;




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
                                Fiyat yeniFiyat= new Fiyat();

                                //if (siteProduct.PrimaryStoreCurrencyCode == "USD")
                                //{
                                //    yeniFiyat.DovizCinsi = Currencies.USD;
                                //}
                                //else if (siteProduct.PrimaryStoreCurrencyCode == "GBP")
                                //{
                                //    yeniFiyat.DovizCinsi = Currencies.GBP;
                                //}
                                //else if (siteProduct.PrimaryStoreCurrencyCode == "EUR")
                                //{
                                //    yeniFiyat.DovizCinsi = Currencies.EUR;
                                //}
                                //else if (siteProduct.PrimaryStoreCurrencyCode == "AUD")
                                //{
                                //    yeniFiyat.DovizCinsi = Currencies.AUD;
                                //}
                                //else
                                //{
                                //    yeniFiyat.DovizCinsi = Currencies.USD;
                                //}
                                yeniFiyat.DovizCinsi = Currencies.USD;

                                yeniFiyat.YeniFiyat = siteProduct.Price;
                               yeniFiyat.OlusturmaTarihi=DateTime.Now;


                                yeni.SatisFiyati = yeniFiyat;
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


                                                    yeniVaryasyonlar.Sku = siteProduct.Sku;

                                                    string varyasyonTuru = productAttribute.Name.ToLower();
                                                    string varyasyonDegeri = productAttributeValue.Name.ToLower();

                                                    if (productAttributeValue.PictureId.GetValueOrDefault() != 0)
                                                    {
                                                        try
                                                        {
                                                            var picture = await _pictureService.GetPictureByIdAsync(productAttributeValue.PictureId.GetValueOrDefault());

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

                                                if (productAttributeValue.PriceAdjustment != 0)
                                                {
                                                    Fiyat yeniVaryasyonFiyat = new Fiyat();

                                                    //if (siteProduct.PrimaryStoreCurrencyCode == "USD")
                                                    //{
                                                    //    yeniVaryasyonFiyat.DovizCinsi = Currencies.USD;
                                                    //}
                                                    //else if (siteProduct.PrimaryStoreCurrencyCode == "GBP")
                                                    //{
                                                    //    yeniVaryasyonFiyat.DovizCinsi = Currencies.GBP;
                                                    //}
                                                    //else if (siteProduct.PrimaryStoreCurrencyCode == "EUR")
                                                    //{
                                                    //    yeniVaryasyonFiyat.DovizCinsi = Currencies.EUR;
                                                    //}
                                                    //else if (siteProduct.PrimaryStoreCurrencyCode == "AUD")
                                                    //{
                                                    //    yeniVaryasyonFiyat.DovizCinsi = Currencies.AUD;
                                                    //}
                                                    //else
                                                    //{
                                                    //    yeniVaryasyonFiyat.DovizCinsi = Currencies.USD;
                                                    //}
yeniVaryasyonFiyat.DovizCinsi = Currencies.USD;
                                                    yeniVaryasyonFiyat.OlusturmaTarihi =
                                                        pr.Where(x => x.Id == siteProduct.Id).First().CreatedOnUtc;


                                                    yeniVaryasyonFiyat.YeniFiyat= productAttributeValue.PriceAdjustment;




                                                    yeniVaryasyonlar.SatisFiyati = yeniVaryasyonFiyat;
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
                                                      
                                                            yeniVaryasyonlar.Adet =
                                                                int.Parse(
                                                                    Regex.Match(varyasyonDegeri, "[0-9]+").Value);

                                                        

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
                                else
                                {
                                    siparisZamani = order.CreatedOnUtc.AddHours(3);
                                }

                                var siparisurunleri = await _orderService.GetOrderItemsAsync(order.Id);

                                foreach (var siparisUrunu in siparisurunleri)
                                {

                                    
                                    var sku = productList.Data.Where(x => x.Id == siparisUrunu.ProductId);
                                    if (sku.Any())
                                    {

                                        
                                        var siparis = new Siparisler();
                                        siparis.Adet = siparisUrunu.Quantity;
                                      
                                     
                                        var yeniFiyat = new Fiyat();
                                        yeniFiyat.YeniFiyat = siparisUrunu.UnitPriceExclTax;
                                        yeniFiyat.OlusturmaTarihi = siparis.SiparisTarih;
                                        //if (order.CustomerCurrencyCode == "USD")
                                        //{
                                        //    yeniFiyat.DovizCinsi = Currencies.USD;
                                        //}
                                        //else if (order.CustomerCurrencyCode == "GBP")
                                        //{
                                        //    yeniFiyat.DovizCinsi = Currencies.GBP;
                                        //}
                                        //else if (order.CustomerCurrencyCode == "EUR")
                                        //{
                                        //    yeniFiyat.DovizCinsi = Currencies.EUR;
                                        //}
                                        //else if (order.CustomerCurrencyCode == "AUD")
                                        //{
                                        //    yeniFiyat.DovizCinsi = Currencies.AUD;
                                        //}
                                        //else if (order.CustomerCurrencyCode == "TRY")
                                        //{
                                        //    yeniFiyat.DovizCinsi = Currencies.TRY;
                                        //}
                                        //else
                                        //{
                                        //    yeniFiyat.DovizCinsi = Currencies.USD;
                                        //}
                                        yeniFiyat.DovizCinsi = Currencies.USD;

                                        siparis.SatisFiyati = yeniFiyat;



                                        siparis.ReceiptId = order.Id;
                                        siparis.SiparisDurumu = order.OrderStatus.ToString();
                                        siparis.SiparisTarih = siparisZamani;
                                        siparis.Tarih = DateTime.Now;
                                        siparis.TransacationId = siparisUrunu.Id;
                                        siparis.Sku = sku.FirstOrDefault().Sku;
                                        var varyasonList=new List<Varyasyonlar>();
                                        try
                                        {
                                            //Todo:attributelar siliniyor ama orderda kalıyor bu durumda execption alıyoruz bununla ilgili çözüm düşün
                                            //örnek:
                                            //SELECT *
                                            //FROM[HoodArcheryShopDb].[dbo].[Product_ProductAttribute_Mapping] where Id = 445
                                            //SELECT*
                                            //    FROM[HoodArcheryShopDb].[dbo].[ProductAttributeValue] where Id = 3206
                                            if (!string.IsNullOrEmpty(siparisUrunu.AttributesXml))
                                            {
                                                string attributesXml = siparisUrunu.AttributesXml;
                                                string decAttTxt =
                                                    _htmlFormatter.ConvertHtmlToPlainText(
                                                        siparisUrunu.AttributeDescription, true, true);




                                                XmlDocument document = new XmlDocument();
                                                document.LoadXml(attributesXml);

                                                var nodes = document.GetElementsByTagName("ProductAttribute");
                                                int nodeSira = 0;
                                                foreach (XmlElement node in nodes)
                                                {
                                                    try
                                                    {
                                                        Varyasyonlar yeniVaryasyonlar = new Varyasyonlar();

                                                        var productAttributeMappingId =
                                                            int.Parse(node.Attributes[0].InnerText);

                                                     
                                                        int productAttributValueId;

                                                        bool success = int.TryParse(node.InnerText,out productAttributValueId);

                                                       

                                                        var productAttributemapping =
                                                            await _productAttributeService
                                                                .GetProductAttributeMappingByIdAsync(
                                                                    productAttributeMappingId);
                                                        string varyasyonTuru = "";
                                                        string varyasyonDegeri ="";

                                                        if (productAttributemapping!=null)
                                                        {
                                                            var productAttribute =
                                                                await _productAttributeService.GetProductAttributeByIdAsync(
                                                                    productAttributemapping.ProductAttributeId);

                                                            if (productAttribute==null)
                                                            {
                                                                string attnullnapcaz = "düşün";
                                                            }
                                                            varyasyonTuru = productAttribute.Name.ToLower();
                                                            
                                                            if (success)
                                                            {
                                                                var productAttributeValue =
                                                                    await _productAttributeService
                                                                        .GetProductAttributeValueByIdAsync(
                                                                            productAttributValueId);
                                                                if (productAttributeValue!=null)
                                                                {
                                                                    varyasyonDegeri = productAttributeValue.Name.ToLower();
                                                                    if (productAttributeValue.PictureId.GetValueOrDefault() != 0)
                                                                    {
                                                                        try
                                                                        {
                                                                            var picture =
                                                                                await _pictureService.GetPictureByIdAsync(
                                                                                    productAttributeValue.PictureId.GetValueOrDefault());

                                                                            if (picture != null)
                                                                            {
                                                                                string url =
                                                                                    await _pictureService.GetPictureUrlAsync(
                                                                                        picture.Id);

                                                                                yeniVaryasyonlar.FotografLinki = url;
                                                                            }


                                                                        }
                                                                        catch
                                                                        {

                                                                            yeniVaryasyonlar.FotografLinki = null;
                                                                        }


                                                                    }

                                                                    //eğer varyason fiyat farkı değeri sipariş içindekinden farklı ise siparişteki değeri kullan

                                                                    List<string> varyasonPlainList =
                                                                        decAttTxt.Split("\n").ToList();
                                                                    var ilgiliVaryasyonText = varyasonPlainList[nodeSira];

                                                                    if (ilgiliVaryasyonText.Contains(" ["))
                                                                    {
                                                                        //Length: 5" Ottoman Shape [+6 C$]
                                                                        //demekki bu varyasyonda ücret farkı var, ücret farkını alıp sadece varyasyon adı bırakalım
                                                                        string birimliDeger =
                                                                            ilgiliVaryasyonText.Split(" [")[1]
                                                                                .Replace("]", "").Replace(",",".");
                                                                        
                                                                        decimal birim = 1;
                                                                        if (birimliDeger.Contains("-"))
                                                                        {
                                                                            birim = -1;
                                                                        }

                                                                    

                                                                        decimal price = 0;
                                                                        decimal.TryParse(Regex.Match(birimliDeger, "[0-9,\\.]+").Value, out price);
                                                                        price = birim * price;

                                                                        if (productAttributeValue.PriceAdjustment != price)
                                                                        {
                                                                          Console.WriteLine("Hata: Varyasyon Fiyat Farkı :"+price+"/"+ productAttributeValue.PriceAdjustment+"VaryasyonValueId:"+ productAttributeValue.Id);
                                                                        }

                                                                        Fiyat yeniVaryasyonFiyat = new Fiyat();

                                                                        yeniVaryasyonFiyat.DovizCinsi =
                                                                            yeniFiyat.DovizCinsi;
                                                                        yeniVaryasyonFiyat.YeniFiyat = price;
                                                                        yeniVaryasyonFiyat.OlusturmaTarihi =
                                                                            siparis.SiparisTarih;
                                                                        yeniVaryasyonlar.SatisFiyati = yeniVaryasyonFiyat;
                                                                        


                                                                        ilgiliVaryasyonText =
                                                                            ilgiliVaryasyonText.Split(" [")[0];
                                                                    }




                                                                    



                                                                }
                                                                else
                                                                {
                                                                    //bu varyasyon value değeri DB'de yok o yüzden baştan oluşturacağız.

                                                                    List<string> varyasonPlainList =
                                                                        decAttTxt.Split("\n").ToList();
                                                                    var ilgiliVaryasyonText = varyasonPlainList[nodeSira];


                                                                    if (ilgiliVaryasyonText.Contains(" ["))
                                                                    {
                                                                        //Length: 5" Ottoman Shape [+6 C$]
                                                                        //demekki bu varyasyonda ücret farkı var, ücret farkını alıp sadece varyasyon adı bırakalım
                                                                        string birimliDeger =
                                                                            ilgiliVaryasyonText.Split(" [")[1]
                                                                                .Replace("]", "").Replace(",", ".");

                                                                        decimal birim = 1;
                                                                        if (birimliDeger.Contains("-"))
                                                                        {
                                                                            birim = -1;
                                                                        }


                                                                        decimal price = 0;
                                                                        decimal.TryParse(Regex.Match(birimliDeger, "[0-9,\\.]+").Value, out price);
                                                                        price = birim * price;

                                                                        Fiyat yeniVaryasyonFiyat = new Fiyat();
                                                                        yeniVaryasyonFiyat.DovizCinsi =
                                                                            yeniFiyat.DovizCinsi;
                                                                        yeniFiyat.OlusturmaTarihi =
                                                                            siparis.SiparisTarih;
                                                                        yeniVaryasyonFiyat.YeniFiyat = price;
                                                                       
                                                                        yeniVaryasyonlar.SatisFiyati =
                                                                            yeniVaryasyonFiyat;
                                                                        ilgiliVaryasyonText =
                                                                            ilgiliVaryasyonText.Split(" [")[0];
                                                                    }


                                                                    var ilgiliVaryasyonTextList =
                                                                        ilgiliVaryasyonText.Split(": ");
                                                                    if (productAttribute==null)
                                                                    {
                                                                        varyasyonTuru = ilgiliVaryasyonTextList[0].ToLower();
                                                                    }
                                                                  

                                                                    string varDegeri = "";
                                                                    for (int k = 0; k < ilgiliVaryasyonTextList.Length; k++)
                                                                    {
                                                                        if (k > 0)
                                                                        {
                                                                            varDegeri += ilgiliVaryasyonTextList[k];
                                                                        }
                                                                        if (k > 1)
                                                                        {
                                                                            varDegeri += ":" + ilgiliVaryasyonTextList[k];
                                                                        }

                                                                    }

                                                                    if (varDegeri == "")
                                                                    {
                                                                        varDegeri = ilgiliVaryasyonTextList[1].ToLower();
                                                                    }

                                                                    varyasyonDegeri = varDegeri.ToLower();
                                                                }




                                                                yeniVaryasyonlar.NopCommerceVaryasyonValueId =
                                                                    productAttributValueId;


                                                            }
                                                            else
                                                            {
                                                                varyasyonDegeri = node.InnerText;
                                                                yeniVaryasyonlar.NopCommerceVaryasyonValueId = null;
                                                            }










                                                            yeniVaryasyonlar.VaryasyonTuru =
                                                                productAttribute.Name.ToLower();
                                                            yeniVaryasyonlar.VaryasyonAdi = varyasyonDegeri.ToLower();
                                                            yeniVaryasyonlar.NopCommerceVaryasyonId =
                                                                productAttribute.Id;
                                                        }
                                                        else
                                                        {
                                                            string status = "null";

                                                            //bu varyasyon DB'de yok o yüzden baştan oluşturacağız.
                                                            List<string> varyasonPlainList =
                                                                decAttTxt.Split("\n").ToList();
                                                            var ilgiliVaryasyonText= varyasonPlainList[nodeSira];


                                                            if (ilgiliVaryasyonText.Contains(" ["))
                                                            {
                                                                //Length: 5" Ottoman Shape [+6 C$]
                                                                //demekki bu varyasyonda ücret farkı var, ücret farkını alıp sadece varyasyon adı bırakalım
                                                                string birimliDeger =
                                                                    ilgiliVaryasyonText.Split(" [")[1]
                                                                        .Replace("]", "").Replace(",", ".");

                                                                decimal birim = 1;
                                                                if (birimliDeger.Contains("-"))
                                                                {
                                                                    birim = -1;
                                                                }


                                                                decimal price = 0;
                                                                decimal.TryParse(Regex.Match(birimliDeger, "[0-9,\\.]+").Value, out price);
                                                                price = birim * price;

                                                                Fiyat yeniVaryasyonFiyat = new Fiyat();
                                                                yeniVaryasyonFiyat.DovizCinsi = yeniFiyat.DovizCinsi;
                                                                yeniVaryasyonFiyat.OlusturmaTarihi =
                                                                    siparis.SiparisTarih;
                                                                yeniVaryasyonFiyat.YeniFiyat = price;


                                                                yeniVaryasyonlar.SatisFiyati = yeniVaryasyonFiyat;

                                                                ilgiliVaryasyonText =
                                                                    ilgiliVaryasyonText.Split(" [")[0];
                                                            }


                                                            var ilgiliVaryasyonTextList =
                                                                ilgiliVaryasyonText.Split(": ");
                                                            varyasyonTuru = ilgiliVaryasyonTextList[0].ToLower();
                                                            
                                                            string varDegeri = "";
                                                            for (int k = 0; k < ilgiliVaryasyonTextList.Length; k++)
                                                            {
                                                                if (k>0)
                                                                {
                                                                    varDegeri += ilgiliVaryasyonTextList[k];
                                                                }
                                                                if (k > 1)
                                                                {
                                                                    varDegeri += ":" + ilgiliVaryasyonTextList[k];
                                                                }

                                                            }

                                                            if (varDegeri=="")
                                                            {
                                                                varDegeri = ilgiliVaryasyonTextList[1].ToLower();
                                                            }

                                                            varyasyonDegeri = varDegeri.ToLower();



                                                            //yeniVaryasyonlar.NopCommerceVaryasyonValueId =
                                                            //    productAttributeValue.Id;
                                                            //yeniVaryasyonlar.NopCommerceVaryasyonId =
                                                            //    productAttribute.Id;
                                                            
                                                           
                                                            yeniVaryasyonlar.NopCommerceVaryasyonValueId = null;





                                                            yeniVaryasyonlar.VaryasyonTuru = varyasyonTuru;
                                                        yeniVaryasyonlar.VaryasyonAdi = varyasyonDegeri;
                                                       






                                                    }
                                                          

                                                       
                                                       


                                                        yeniVaryasyonlar.Sku = sku.FirstOrDefault().Sku;

                                                      

                                                       

                                                        if (varyasyonTuru.Contains("pcs"))
                                                        {
                                                            yeniVaryasyonlar.Tip =
                                                                VaryasyonTipleri.Adet;
                                                            try
                                                            {
                                                                if (varyasyonDegeri
                                                                    .Contains("broadhead")|| varyasyonDegeri
                                                                        .Contains("huntinghead") || varyasyonDegeri
                                                                        .Contains("hunting head"))
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
                                                                                    "").Replace(
                                                                                    "hunting head",
                                                                                    "").Replace(
                                                                                    "huntinghead",
                                                                                    "").Replace(
                                                                                    "+",
                                                                                    "").Replace(
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

                                                        varyasonList.Add(yeniVaryasyonlar);




                                                    }
                                                    catch(Exception ef)
                                                    {
                                                        string jjj = j.ToString();
                                                        string skku = sku.First().ToString();
                                                        string varId = attributesXml;
                                                     
                                                      
                                                     

                                                    }

                                                    nodeSira +=1;



                                                }
                                            }

                                            siparis.VaryasyonList = varyasonList;
                                        }
                                        catch(Exception e)
                                        {
                                            

                                        }
                                            

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
