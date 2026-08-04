using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using System.Collections.Generic;
using System.Diagnostics;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Services.Catalog;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Data;
using Nop.Services.Common;
using StateProvince = Nop.Core.Domain.Directory.StateProvince;
using System.IO;
using DocumentFormat.OpenXml.Presentation;
using Nop.Plugin.Misc.EtsyToNopcommerce.Domains;
using Nop.Plugin.Misc.EtsyToNopcommerce.Services;
using Nop.Services.Media;
using Picture = Nop.Core.Domain.Media.Picture;
using Nop.Core.Domain.Catalog;
using Nop.Plugin.Misc.EtsyToNopcommerce.Models.Etsy.Listings;
using Nop.Services.Seo;
using Nop.Web.Factories;
using Nop.Plugin.Misc.EtsyToNopcommerce.Infrastructure;


namespace Nop.Plugin.Misc.EtsyToNopcommerce.Controllers
{

    [Area(AreaNames.ADMIN)]
    [RequestFormLimits(ValueCountLimit = int.MaxValue)]
    public class EtsyListingsController : BasePluginController
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
        private readonly IEtsyApiService _etsyApiService;
        #endregion

        #region Ctor

        public EtsyListingsController(ILocalizationService localizationService,
            INotificationService notificationService,
            IPermissionService permissionService,
            ISettingService settingService,
            IStoreContext storeContext,
           ICustomerService customerService,IProductService productService,ICountryService countryService, IAddressService addressService,
            IRepository<StateProvince> stateProvinceRepository, IRepository<Address> addressRepository, IPictureService pictureService,
            ICustomProductReviewMappingService customProductReviewMappingService, CatalogSettings catalogSettings, IBackgroundQueue queue,
            IUrlRecordService urlRecordService, ICustomerRegistrationService customerRegistrationService, ICustomerModelFactory customerModelFactory,
            IGenericAttributeService genericAttributeService, CustomerSettings customerSettings, IProductReviewsEtsyReviewService etsyReviewService, 
            IProductReviewsTransactionsMappingService productReviewsTransactionsMappingService, IEtsyApiService etsyApiService)
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
            _etsyApiService=etsyApiService;
        }

        #endregion



        #region Methods



        public async Task<string> InsertReviewMedia(string ProductSeName, UploadDataBinary data, int reviewId)
        {

            string name = ProductSeName + "-" + DateTime.UtcNow.ToFileTime();
            Stopwatch sw = new Stopwatch();

            var pic = new Picture();


            var vid = new Video();
            if (data.Extentions.Contains("image"))
            {
                try
                {
                    sw.Start();

                    var raw = WebpEncoder.Encode(data.BinaryData);
                    sw.Stop();
                    Console.WriteLine("Elapsed Picture Encode={0}", sw.Elapsed);
                    pic = await _pictureService.InsertPictureAsync(raw, "image/webp", name);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message + Environment.NewLine);
                }
            }
            //else if (data.Extentions.Contains("video"))
            //{
            //    try
            //    {
            //        vid = await _videoService.InsertVideoAsync(data.BinaryData, name, data.Extentions);
            //    }
            //    catch (Exception e)
            //    {
            //        System.IO.File.AppendAllText(@"customProductReview.log", e.InnerException + Environment.NewLine);
            //    }
            //}

            int? lastPicId = pic.Id;
            int? lastVidId = 0;
            if (lastPicId == 0)
            {
                lastPicId = null;
            }

            if (lastVidId == 0)
            {
                lastVidId = null;
            }


            if (!(lastPicId == null && lastVidId == null))
            {
                await _customProductReviewMappingService.InsertCustomProductReviewMappingAsync(reviewId, lastPicId,
                    lastVidId);
            }

            return "done";
        }

        
        public async Task<IActionResult> InsertEtsyListingsToNopcommerce(IList<EtsyListing> fromDelist)
        {

            var storeId = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var settings = await _settingService.LoadSettingAsync<EtsyToNopcommerceSettings>(storeId);
            var currentStore = await _storeContext.GetCurrentStoreAsync();
            if (settings != null)
            {
                storeId = currentStore.Id;
                int reviewsAdded = 0;
                int reviewsWithPhotoAdded = 0;

                _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));
               


                //Buraya receipts çağırılıp sku kontröllü review filtrelemesi yapacağız
                var receipts = await _etsyApiService.GetAllEtsyReceipts();


                foreach (var listing in fromDelist)
                {
                 //   //Todo: try catch eklendi ama hala hata var çöz
                 //   try
                 //   {
                 //       var resultTransactionMapping=  await _productReviewsTransactionsMappingService.GetProductReviewsTransactionsMappingByTransactionIdAsync(review.TransactionId.Value);
                 
                 //if (resultTransactionMapping.Count>0)
                 //{
                 //    //bu review daha önce eklenmiş ona göre update işlemi vs. yap

                 //}
                 //else
                 //{
                 //           //ilk olarak ilgili yorum hangi ürüne ait skusunu bulup siteden ilgili ürünü bulmak lazım

                 //           var ilgiliReceipt = receipts.Single(x => x.Transactions.Any(y => y.TransactionId == review.TransactionId));
                 //           Random rnd = new Random();

                 //           if (ilgiliReceipt != null)
                 //           {
                 //               if (ilgiliReceipt.BuyerEmail.IsNullOrEmpty())
                 //               {
                 //                   ilgiliReceipt.BuyerEmail = "test_" + rnd.Next(1000, 9999) + "@site.com";
                 //               }
                 //               string ilgiliSku =
                 //                   (from m in ilgiliReceipt.Transactions
                 //                    where m.TransactionId == review.TransactionId
                 //                    select (string)m.Sku)
                 //                   .FirstOrDefault();
                 //               var ilgiliNopcommerceUrun = await _productService.GetProductBySkuAsync(ilgiliSku);

                 //               if (ilgiliNopcommerceUrun != null)
                 //               {
                 //                   //bu yorum bu ürün için daha önce yapılmışmı kontrol et
                 //                   var productReviewsByproductId =
                 //                       await _productService.GetAllProductReviewsAsync(productId: ilgiliNopcommerceUrun.Id);
                 //                   if (productReviewsByproductId != null)
                 //                   {
                 //                       var buMusteriDahaOnceKayitOlduMu = (await _customerService.GetAllCustomersAsync(email: ilgiliReceipt.BuyerEmail)).ToList()
                 //                           .Any(x => x.Email.Contains(ilgiliReceipt.BuyerEmail));


                 //                       //eğer müşteri maili daha önce kayıt edilmediyse yeni müşteri kadı oluşturulacak
                 //                       if (!buMusteriDahaOnceKayitOlduMu)
                 //                       {

                 //                           Customer newEtsyCustomer = new Customer();



                 //                           var yorumTarihi = int.Parse(review.CreatedTimestamp.ToString())
                 //                               .UnixTimeStampToDateTime().AddDays(-rnd.Next(30, 600));

                 //                           var musteriGeneratedEmail
                 //                               = rnd.Next(1000, 9999) + "_" + ilgiliReceipt.BuyerEmail;
                 //                           newEtsyCustomer.Email = musteriGeneratedEmail;
                 //                           newEtsyCustomer.Active = true;
                 //                           newEtsyCustomer.CreatedOnUtc = yorumTarihi;
                 //                           newEtsyCustomer.AdminComment = "EtsyToNopcommerceGeneratedCustomer";
                 //                           newEtsyCustomer.LastActivityDateUtc = yorumTarihi.AddDays(rnd.Next(3, 600));
                 //                           newEtsyCustomer.LastLoginDateUtc = yorumTarihi.AddDays(rnd.Next(3, 600));
                 //                           newEtsyCustomer.RegisteredInStoreId = storeId;


                 //                           await _customerService.InsertCustomerAsync(newEtsyCustomer);


                 //                           var kayitliMusteri = await _customerService.GetCustomerByEmailAsync(musteriGeneratedEmail);


                 //                           Address yeniMusteriAdresiAddress = new Address();

                 //                           string firstName = "";
                 //                           string lastName = "";
                 //                           if (ilgiliReceipt.Name.Contains(" "))
                 //                           {
                 //                               firstName = ilgiliReceipt.Name.Split(" ")[0];
                 //                               lastName = ilgiliReceipt.Name.Split(" ")[1];
                 //                           }
                 //                           else
                 //                           {
                 //                               firstName = ilgiliReceipt.Name;
                 //                           }

                 //                           yeniMusteriAdresiAddress.FirstName = firstName;
                 //                           yeniMusteriAdresiAddress.LastName = lastName;
                 //                           yeniMusteriAdresiAddress.Email = musteriGeneratedEmail;
                 //                           yeniMusteriAdresiAddress.CreatedOnUtc = yorumTarihi;
                 //                           yeniMusteriAdresiAddress.Address1 = ilgiliReceipt.FirstLine;
                 //                           yeniMusteriAdresiAddress.Address2 = ilgiliReceipt.SecondLine;
                 //                           yeniMusteriAdresiAddress.City = ilgiliReceipt.City;
                 //                           yeniMusteriAdresiAddress.ZipPostalCode = ilgiliReceipt.Zip;


                 //                           var ilgiliCountry = await _countryService.GetCountryByTwoLetterIsoCodeAsync(ilgiliReceipt.CountryIso);
                 //                           var ilgiliStateVarmi = await _stateProvinceRepository.Table.AnyAsync(x =>
                 //                               x.Name.ToLower() == ilgiliReceipt.State.ToLower() && x.CountryId == ilgiliCountry.Id);
                 //                           bool ilgliStateKisaltilmismi = false;
                 //                           if (!ilgiliStateVarmi)
                 //                           {
                 //                               ilgiliStateVarmi = await _stateProvinceRepository.Table.AnyAsync(x => x.Abbreviation == ilgiliReceipt.State && x.CountryId == ilgiliCountry.Id);
                 //                               if (ilgiliStateVarmi)
                 //                               {
                 //                                   ilgliStateKisaltilmismi = true;

                 //                               }
                 //                           }


                 //                           if (ilgiliStateVarmi)
                 //                           {
                 //                               if (ilgliStateKisaltilmismi)
                 //                               {
                 //                                   var ilgiliState = await _stateProvinceRepository.Table.SingleAsync(x => x.Abbreviation == ilgiliReceipt.State && x.CountryId == ilgiliCountry.Id);
                 //                                   yeniMusteriAdresiAddress.StateProvinceId = ilgiliState.Id;
                 //                               }
                 //                               else
                 //                               {
                 //                                   var ilgiliState = await _stateProvinceRepository.Table.SingleAsync(x =>
                 //                                       x.Name.ToLower() == ilgiliReceipt.State.ToLower() && x.CountryId == ilgiliCountry.Id);
                 //                                   yeniMusteriAdresiAddress.StateProvinceId = ilgiliState.Id;
                 //                               }

                 //                           }


                 //                           yeniMusteriAdresiAddress.CountryId = ilgiliCountry.Id;
                 //                           await _addressService.InsertAddressAsync(yeniMusteriAdresiAddress);

                 //                           var kayitliMusteriAdresi = await _addressRepository.Table.SingleAsync(x => x.Email == musteriGeneratedEmail);
                 //                           await _customerService.InsertCustomerAddressAsync(kayitliMusteri, kayitliMusteriAdresi);

                 //                           kayitliMusteri.Active = true;
                 //                           kayitliMusteri.Username = kayitliMusteri.Email;
                 //                           kayitliMusteri.BillingAddressId = kayitliMusteriAdresi.Id;
                 //                           kayitliMusteri.ShippingAddressId = kayitliMusteriAdresi.Id;

                 //                           await _customerService.UpdateCustomerAsync(kayitliMusteri);

                 //                           //form fields


                 //                           if (_customerSettings.FirstNameEnabled)
                 //                               await _genericAttributeService.SaveAttributeAsync(kayitliMusteri, NopCustomerDefaults.FirstNameAttribute, kayitliMusteriAdresi.FirstName);
                 //                           if (_customerSettings.LastNameEnabled)
                 //                               await _genericAttributeService.SaveAttributeAsync(kayitliMusteri, NopCustomerDefaults.LastNameAttribute, kayitliMusteriAdresi.LastName);

                 //                       }

                 //                       var currentCustomer = (await _customerService.GetAllCustomersAsync(email: ilgiliReceipt.BuyerEmail)).ToList()
                 //                           .Single(x => x.Email.Contains(ilgiliReceipt.BuyerEmail));


                 //                       //yorum ekleme burada yapılacak

                 //                       var rating = review.Rating.Value;
                 //                       if (rating < 1 || rating > 5)
                 //                           rating = _catalogSettings.DefaultProductRatingValue;
                 //                       var isApproved = !_catalogSettings.ProductReviewsMustBeApproved;
                 //                       var customer = currentCustomer;

                 //                       var productReview = new ProductReview
                 //                       {
                 //                           ProductId = ilgiliNopcommerceUrun.Id,
                 //                           CustomerId = customer.Id,
                 //                           Title = "",
                 //                           ReviewText = review.Review,
                 //                           Rating = rating,
                 //                           HelpfulYesTotal = rnd.Next(1, 7),
                 //                           HelpfulNoTotal = 0,
                 //                           IsApproved = true,
                 //                           CreatedOnUtc = int.Parse(review.CreatedTimestamp.ToString())
                 //                               .UnixTimeStampToDateTime(),
                 //                           StoreId = storeId,
                 //                       };
                 //                       await _productService.InsertProductReviewAsync(productReview);
                 //                       reviewsAdded += 1;
                 //                       Console.WriteLine("Toplam " + reviewsAdded + " adet yorum eklendi");
                 //                       var reviewId = productReview.Id;




                 //                       //update product review totals
                 //                       await _productService.UpdateProductReviewTotalsAsync(ilgiliNopcommerceUrun);


                 //                       //Add ProductReview Etsy Review Mapping
                 //                       await _productReviewsTransactionsMappingService
                 //                           .InsertProductReviewsTransactionsMappingAsync(reviewId,
                 //                               review.TransactionId.Value, review.Id);

                 //                       #region Product Review Media Upload Section

                 //                       try
                 //                       {


                 //                           //pictures
                 //                           List<UploadDataBinary> dataList = new List<UploadDataBinary>();

                 //                           if (review.ImageUrlFullxfull != null)
                 //                           {
                 //                               WebClient client = new WebClient();

                 //                               byte[] bytes = await client.DownloadDataTaskAsync(review.ImageUrlFullxfull);

                 //                               var uploadData = new UploadDataBinary();
                 //                               uploadData.BinaryData = bytes;
                 //                               uploadData.Extentions = "image";

                 //                               dataList.Add(uploadData);

                 //                               //string fileName = "tempUpload"+DateTime.UtcNow.ToFileTime() + fileInfo.Extension;

                 //                               var productSeName = await _urlRecordService.GetSeNameAsync(ilgiliNopcommerceUrun, currentStore.DefaultLanguageId);
                 //                               foreach (var data in dataList)
                 //                               {
                 //                                   _queue.QueueTask(async token =>
                 //                                   {
                 //                                       reviewsWithPhotoAdded += 1;

                 //                                       Console.WriteLine("Toplam " + reviewsWithPhotoAdded + " adet fotograflı yorum eklendi");
                 //                                       await InsertReviewMedia(productSeName, data, reviewId);
                 //                                   });
                 //                               }
                 //                           }


                 //                       }
                 //                       catch (Exception e)
                 //                       {
                 //                           Console.WriteLine(e);

                 //                       }
                 //                       #endregion



                 //                   }
                 //               }
                 //    }





                 //       }
                 //   }
                 //   catch (Exception e )
                 //   {

                 //       Console.WriteLine(e);
                 //   }
                }

             
            
                _notificationService.SuccessNotification(reviewsAdded + " adet yorum, " + reviewsWithPhotoAdded + " adet fotograflı yorum eklendi");
                

                return RedirectToRoute("Plugin.Misc.EtsyToNopcommerce.Configure");
            }
            else
            {
                return RedirectToRoute("Plugin.Misc.EtsyToNopcommerce.Configure");
            }
        }



        
    }
        #endregion
    }
