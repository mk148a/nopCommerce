using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.FileProviders;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Localization;
using Nop.Core.Domain.Media;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Security;
using Nop.Core.Domain.Shipping;
using Nop.Core.Events;
using Nop.Core.Infrastructure;
using Nop.Data;
using Nop.Plugin.Widgets.CustomProductReviews.Domains;
using Nop.Plugin.Widgets.CustomProductReviews.Services;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Html;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Media;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Security;
using Nop.Services.Seo;
using Nop.Services.Stores;
using Nop.Web.Factories;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

using System.Runtime.InteropServices;
using DocumentFormat.OpenXml.Drawing;
using Nop.Web.Models.Catalog;
using Nop.Web.Framework.Mvc.Routing;
using Nop.Plugin.Widgets.CustomProductReviews.Components;
using DocumentFormat.OpenXml.Presentation;
using MimeDetective;
using Picture = Nop.Core.Domain.Media.Picture;

namespace Nop.Plugin.Widgets.CustomProductReviews.Controllers
{

    [AutoValidateAntiforgeryToken]
    public class CustomProductReviewsController : BasePluginController
    {

        #region Fields

        private readonly CaptchaSettings _captchaSettings;
        private readonly CatalogSettings _catalogSettings;
        private readonly ICustomerActivityService _customerActivityService;
        private readonly ICustomerService _customerService;
        private readonly IEventPublisher _eventPublisher;
        private readonly ILocalizationService _localizationService;
        private readonly IOrderService _orderService;
        private readonly IProductModelFactory _productModelFactory;
        private readonly IProductService _productService;
        private readonly IReviewTypeService _reviewTypeService;
        private readonly IStoreContext _storeContext;
        private readonly IUrlRecordService _urlRecordService;
        private readonly IWorkContext _workContext;
        private readonly IWorkflowMessageService _workflowMessageService;
        private readonly LocalizationSettings _localizationSettings;
        private readonly IPictureService _pictureService;
        private readonly IProductReviewVideoService _videoService;
        private readonly ICustomProductReviewMappingService _customProductReviewMappingService;
        private readonly IBackgroundQueue _queue;
        protected readonly INotificationService _notificationService;
        protected readonly INopUrlHelper _nopUrlHelper;



        #endregion

        #region Ctor

        public CustomProductReviewsController(CaptchaSettings captchaSettings,
            CatalogSettings catalogSettings,
            ICustomerActivityService customerActivityService,
            ICustomerService customerService,
            IEventPublisher eventPublisher,
            ILocalizationService localizationService,
            IOrderService orderService,
            IProductModelFactory productModelFactory,
            IProductService productService,
            IReviewTypeService reviewTypeService,
            IStoreContext storeContext,
            IUrlRecordService urlRecordService,
            IWorkContext workContext,
            IWorkflowMessageService workflowMessageService,
            LocalizationSettings localizationSettings,
            IPictureService pictureService,
            IProductReviewVideoService videoService,
            ICustomProductReviewMappingService customProductReviewMappingService,
            IBackgroundQueue queue,
            INotificationService notificationService,
            INopUrlHelper nopUrlHelper

        )
        {
            _captchaSettings = captchaSettings;
            _pictureService = pictureService;
            _videoService = videoService;
            _catalogSettings = catalogSettings;
            _customerActivityService = customerActivityService;
            _customerService = customerService;
            _eventPublisher = eventPublisher;
            _localizationService = localizationService;
            _orderService = orderService;
            _productModelFactory = productModelFactory;
            _productService = productService;
            _reviewTypeService = reviewTypeService;
            _storeContext = storeContext;
            _urlRecordService = urlRecordService;
            _workContext = workContext;
            _workflowMessageService = workflowMessageService;
            _localizationSettings = localizationSettings;
            _customProductReviewMappingService = customProductReviewMappingService;
            _queue = queue;
            _notificationService=notificationService;
            _nopUrlHelper=nopUrlHelper;

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

        public static bool IsValidFileType(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return false;

            var inspector = new ContentInspectorBuilder()
            {
                Definitions = MimeDetective.Definitions.DefaultDefinitions.All()
            }.Build();


            using (var fileStream = file.OpenReadStream())
            {
                var result = inspector.Inspect(fileStream);
                var mimeType = result.ByMimeType().FirstOrDefault()?.MimeType;

                if (mimeType == null)
                    return false;

                // Sadece resim ve video türlerini kabul et
                return mimeType.StartsWith("image/") || mimeType.StartsWith("video/");
            }
        }

        //[FormValueRequired("add-review")]
        [HttpPost]
        [ValidateCaptcha]
        [RequestFormLimits(MultipartBodyLengthLimit = 1048576000)]
        [RequestSizeLimit(1048576000)]
        public virtual async Task<IActionResult> ProductReviewsAdd(int productId, ProductReviewsModel model, bool captchaValid, List<IFormFile> photos)
        {
            bool fileIsValid = true;
            var settings = EngineContext.Current.Resolve<Nop.Services.Configuration.ISettingService>();
            var reviewSettings = await settings.LoadSettingAsync<CustomProductReviewsSettings>((await _storeContext.GetCurrentStoreAsync()).Id)
                                 ?? await settings.LoadSettingAsync<CustomProductReviewsSettings>();
            var maximumFile = reviewSettings?.MaximumFile > 0 ? reviewSettings.MaximumFile : 5;
            if (photos != null && photos.Count > maximumFile)
            {
                return Json(new
                {
                    Success = false,
                    Message = string.Format(await _localizationService.GetResourceAsync("Plugins.Widgets.CustomProductReviews.MaxFilesInUpload"), maximumFile),
                    IsBackgroundProcess = false
                });
            }
            if (photos != null && photos.Count > 0)
            {
              
                foreach (var file in photos)
                {
                    fileIsValid= IsValidFileType(file);
                    if (!fileIsValid)
                    {
                        break;
                    }
                }
              
            }

            if (!fileIsValid)
            {
                return Json(new
                {
                    Success = false,
                    Message = await _localizationService.GetResourceAsync("Plugins.Widgets.CustomProductReviews.UnsupportedFileFormat"),
                    IsBackgroundProcess = false // Hata durumunda arka plan işlemi yok
                });
            }
           
            var product = await _productService.GetProductByIdAsync(productId);
            var currentStore = await _storeContext.GetCurrentStoreAsync();

            if (product == null || product.Deleted || !product.Published || !product.AllowCustomerReviews ||
                !await _productService.CanAddReviewAsync(product.Id,
                    _catalogSettings.ShowProductReviewsPerStore ? currentStore.Id : 0))
                return RedirectToRoute("Homepage");

            // CAPTCHA kontrolü
            if (_captchaSettings.Enabled && _captchaSettings.ShowOnProductReviewPage && !captchaValid)
            {
                ModelState.AddModelError("", await _localizationService.GetResourceAsync("Common.WrongCaptchaMessage"));
            }

            await ValidateProductReviewAvailabilityAsync(product);

            if (ModelState.IsValid)
            {
                // Yorumu kaydet
                var rating = model.AddProductReview.Rating;
                if (rating < 1 || rating > 5)
                    rating = _catalogSettings.DefaultProductRatingValue;

                var isApproved = !_catalogSettings.ProductReviewsMustBeApproved;
                var customer = await _workContext.GetCurrentCustomerAsync();

                var productReview = new ProductReview
                {
                    ProductId = product.Id,
                    CustomerId = customer.Id,
                    Title = model.AddProductReview.Title,
                    ReviewText = model.AddProductReview.ReviewText,
                    Rating = rating,
                    HelpfulYesTotal = 0,
                    HelpfulNoTotal = 0,
                    IsApproved = isApproved,
                    CreatedOnUtc = DateTime.UtcNow,
                    StoreId = currentStore.Id,
                };

                await _productService.InsertProductReviewAsync(productReview).ConfigureAwait(false);
                var reviewId = productReview.Id;

                // Ek yorum türlerini kaydet
                foreach (var additionalReview in model.AddAdditionalProductReviewList)
                {
                    var additionalProductReview = new ProductReviewReviewTypeMapping
                    {
                        ProductReviewId = productReview.Id,
                        ReviewTypeId = additionalReview.ReviewTypeId,
                        Rating = additionalReview.Rating
                    };

                    await _reviewTypeService.InsertProductReviewReviewTypeMappingsAsync(additionalProductReview).ConfigureAwait(false);
                }

                // Ürün toplamlarını güncelle
                await _productService.UpdateProductReviewTotalsAsync(product);

                // Mağaza sahibini bilgilendir
                if (_catalogSettings.NotifyStoreOwnerAboutNewProductReviews)
                    await _workflowMessageService.SendProductReviewStoreOwnerNotificationMessageAsync(productReview,
                        _localizationSettings.DefaultAdminLanguageId);

                // Aktivite logu
                await _customerActivityService.InsertActivityAsync("PublicStore.AddProductReview",
                    string.Format(
                        await _localizationService.GetResourceAsync("ActivityLog.PublicStore.AddProductReview"),
                        product.Name), product);

                // Event'i tetikle
                if (productReview.IsApproved)
                    await _eventPublisher.PublishAsync(new ProductReviewApprovedEvent(productReview));

                model = await _productModelFactory.PrepareProductReviewsModelAsync(product);
                model.AddProductReview.Title = null;
                model.AddProductReview.ReviewText = null;
                
                var processResult = true;
                // Medya yükleme işlemleri
                try
                {
                    List<UploadDataBinary> dataList = new List<UploadDataBinary>();

                    foreach (var photo in photos ?? new List<IFormFile>())
                    {
                        var uploadData = new UploadDataBinary
                        {
                            Extentions = photo.ContentType
                        };

                        using (var ms = new MemoryStream())
                        {
                            await photo.CopyToAsync(ms);
                            uploadData.BinaryData = ms.ToArray();
                            dataList.Add(uploadData);
                        }
                    }

                  
                    foreach (var data in dataList)
                    {
                        _queue.QueueTask(async token =>
                        {
                            processResult =  await InsertReviewMedia(model, data, reviewId);
                          if (!processResult)
                            {
                                var mapping = await _customProductReviewMappingService.GetCustomProductReviewMappingByProductReviewIdAsync(
                                    reviewId);
                                foreach (var map in mapping)
                                {
                                    if (map.ProductReviewVideoId!=null)
                                    {
                                        var vid =await _videoService.GetVideoByIdAsync(map.ProductReviewVideoId.Value);
                                        await _videoService.DeleteVideoAsync(vid);
                                    }
                                    else if (map.PictureId!=null)
                                    {
                                        var pic= await _pictureService.GetPictureByIdAsync(map.PictureId.Value);
                                        await _pictureService.DeletePictureAsync(pic);
                                      
                                    }

                                   

                                }
                            }

                        });
                       
                      
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    // Genel hata durumu
                    return Json(new
                    {
                        Success = false,
                        Message = e.Message,
                        IsBackgroundProcess = false // Hata durumunda arka plan işlemi yok
                    });
                }

                if (processResult)
                {
                    
                }
                else
                {
                    return Json(new
                    {
                        Success = false,
                        Message = await _localizationService.GetResourceAsync("Plugins.Widgets.CustomProductReviews.UploadFileFormatError"),
                        IsBackgroundProcess = false // Hata durumunda arka plan işlemi yok
                    });
                }
                // Başarılı mesajı göster
                if (!isApproved)
                    _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Reviews.SeeAfterApproving") + Environment.NewLine +
                        " " + await _localizationService.GetResourceAsync("Plugins.Widgets.CustomProductReviews.MediaProcessingStarted") + Environment.NewLine +
                        " " + await _localizationService.GetResourceAsync("Plugins.Widgets.CustomProductReviews.MediaProcessingCompletedAutomatically"));
                else
                    _notificationService.SuccessNotification(
                        await _localizationService.GetResourceAsync("Reviews.SuccessfullyAdded") + Environment.NewLine +
                        " " + await _localizationService.GetResourceAsync("Plugins.Widgets.CustomProductReviews.MediaProcessingStarted") + Environment.NewLine +
                        " " + await _localizationService.GetResourceAsync("Plugins.Widgets.CustomProductReviews.MediaProcessingCompletedAutomatically"));

                var message = isApproved
                    ? await _localizationService.GetResourceAsync("Reviews.SuccessfullyAdded")
                    : await _localizationService.GetResourceAsync("Reviews.SeeAfterApproving");

                return Json(new
                {
                    Success = true,
                    Message = message + Environment.NewLine +
                              await _localizationService.GetResourceAsync("Plugins.Widgets.CustomProductReviews.MediaProcessingStarted") + Environment.NewLine +
                              await _localizationService.GetResourceAsync("Plugins.Widgets.CustomProductReviews.MediaProcessingCompletedAutomatically"),
                    IsBackgroundProcess = true // Başarılı durumda arka plan işlemi var
                });
            }

            // Hata durumunda formu yeniden göster
            model = await _productModelFactory.PrepareProductReviewsModelAsync(product);
            return Json(new
            {
                Success = false,
                Message = await _localizationService.GetResourceAsync("Plugins.Widgets.CustomProductReviews.GeneralError"),
                IsBackgroundProcess = false // Hata durumunda arka plan işlemi yok
            });
        }

        public async Task<IActionResult> GetProductReviews(int productId)
        {
            var product = await _productService.GetProductByIdAsync(productId);
            if (product == null || product.Deleted || !product.Published)
                return NotFound();

            var model = await _productModelFactory.PrepareProductReviewsModelAsync(product);
            return PartialView("_ProductReviews", model);
        }
        public async Task<bool> InsertReviewMedia(ProductReviewsModel model, UploadDataBinary data, int reviewId)
        {
            var product = await _productService.GetProductByIdAsync(model.ProductId);
            var seName = await _urlRecordService.GetSeNameAsync(product);

            string name = seName + "-" + DateTime.UtcNow.ToFileTime();

            var sw = new Stopwatch();

            var pic = new Picture();

            ProductReviewVideo vid = new ProductReviewVideo();
            if (data.Extentions.Contains("image"))
            {
                try
                {
                    sw.Start();

                    using (var image = Image.Load(data.BinaryData))
                    using (var ms = new MemoryStream())
                    {
                       await image.SaveAsync(ms, new WebpEncoder { Quality = 90 });
                        sw.Stop();
                        Console.WriteLine("Elapsed Picture Encode={0}", sw.Elapsed);
                      await  System.IO.File.AppendAllTextAsync(@"ImageProcessPerformace.log", string.Format("Elapsed Picture Encode={0}", sw.Elapsed) + Environment.NewLine);

                        byte[] raw = ms.ToArray();
                        pic = await _pictureService.InsertPictureAsync(raw, "image/webp", name);
                    }
                }
                catch (Exception e)
                {
                   await System.IO.File.AppendAllTextAsync(@"customProductReview.log", e.Message + Environment.NewLine);
                   return false;
                }
            }
            else if (data.Extentions.Contains("video"))
            {
                try
                {
                    vid = await _videoService.InsertVideoAsync(data.BinaryData, name, data.Extentions);
                }
                catch (Exception e)
                {
                    await System.IO.File.AppendAllTextAsync(@"customProductReview.log", e.InnerException + Environment.NewLine);
                    return false;
                }
            }
            else
            {
                return false;
            }

            int? lastPicId = pic.Id;
            int? lastVidId = vid.Id;
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
                await _customProductReviewMappingService.InsertCustomProductReviewMappingAsync(reviewId, lastPicId, lastVidId).ConfigureAwait(false);
                
            }
            else
            {
                return false;
            }

            return true;
        }


        protected virtual async Task ValidateProductReviewAvailabilityAsync(Product product)
        {
            var customer = await _workContext.GetCurrentCustomerAsync();
            if (await _customerService.IsGuestAsync(customer) && !_catalogSettings.AllowAnonymousUsersToReviewProduct)
                ModelState.AddModelError(string.Empty,
                    await _localizationService.GetResourceAsync("Reviews.OnlyRegisteredUsersCanWriteReviews"));

            if (!_catalogSettings.ProductReviewPossibleOnlyAfterPurchasing)
                return;

            var hasCompletedOrders = product.ProductType == ProductType.SimpleProduct
                ? await HasCompletedOrdersAsync(product)
                : await (await _productService.GetAssociatedProductsAsync(product.Id)).AnyAwaitAsync(
                    HasCompletedOrdersAsync);

            if (!hasCompletedOrders)
                ModelState.AddModelError(string.Empty,
                    await _localizationService.GetResourceAsync("Reviews.ProductReviewPossibleOnlyAfterPurchasing"));
        }

        protected virtual async ValueTask<bool> HasCompletedOrdersAsync(Product product)
        {
            var customer = await _workContext.GetCurrentCustomerAsync();
            return (await _orderService.SearchOrdersAsync(customerId: customer.Id,
                productId: product.Id,
                osIds: new List<int> { (int)OrderStatus.Complete },
                pageSize: 1)).Any();
        }

        #endregion



    }
}
