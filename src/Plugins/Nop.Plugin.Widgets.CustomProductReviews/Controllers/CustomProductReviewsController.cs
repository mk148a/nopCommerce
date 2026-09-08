using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using CustomVideoService = Nop.Plugin.Widgets.CustomProductReviews.Services.IVideoService;
using Video = Nop.Plugin.Widgets.CustomProductReviews.Domains.Video;
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
using System.Runtime.InteropServices;
using Nop.Web.Models.Catalog;
using SkiaSharp;

namespace Nop.Plugin.Widgets.CustomProductReviews.Controllers
{

    [AutoValidateAntiforgeryToken]
    public class CustomProductReviewsController : BasePluginController
    {

        #region Fields

        private readonly CaptchaSettings _captchaSettings;
        private readonly CatalogSettings _catalogSettings;
        private readonly IAclService _aclService;
        private readonly ICompareProductsService _compareProductsService;
        private readonly ICustomerActivityService _customerActivityService;
        private readonly ICustomerService _customerService;
        private readonly IEventPublisher _eventPublisher;
        private readonly IHtmlFormatter _htmlFormatter;
        private readonly ILocalizationService _localizationService;
        private readonly IOrderService _orderService;
        private readonly IPermissionService _permissionService;
        private readonly IProductAttributeParser _productAttributeParser;
        private readonly IProductModelFactory _productModelFactory;
        private readonly IProductService _productService;
        private readonly IRecentlyViewedProductsService _recentlyViewedProductsService;
        private readonly IReviewTypeService _reviewTypeService;
        private readonly IShoppingCartModelFactory _shoppingCartModelFactory;
        private readonly IShoppingCartService _shoppingCartService;
        private readonly IStoreContext _storeContext;
        private readonly IStoreMappingService _storeMappingService;
        private readonly IUrlRecordService _urlRecordService;
        private readonly IWebHelper _webHelper;
        private readonly IWorkContext _workContext;
        private readonly IWorkflowMessageService _workflowMessageService;
        private readonly LocalizationSettings _localizationSettings;
        private readonly ShoppingCartSettings _shoppingCartSettings;
        private readonly ShippingSettings _shippingSettings;
        private readonly IPictureService _pictureService;
        private readonly CustomVideoService _videoService;
        private readonly ICustomProductReviewMappingService _customProductReviewMappingService;
        private readonly INopFileProvider _fileProvider;
        private readonly IBackgroundQueue _queue;
        private readonly CustomProductReviewsSettings _customProductReviewsSettings;

        #endregion

        #region Ctor

        public CustomProductReviewsController(CaptchaSettings captchaSettings,
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
            IProductModelFactory productModelFactory,
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
            CustomVideoService videoService,
            INopFileProvider fileProvider,
            ShippingSettings shippingSettings,
            ICustomProductReviewMappingService customProductReviewMappingService,
            IBackgroundQueue queue,
            CustomProductReviewsSettings customProductReviewsSettings

        )
        {
            _captchaSettings = captchaSettings;
            _pictureService = pictureService;
            _videoService = videoService;
            _fileProvider = fileProvider;
            _catalogSettings = catalogSettings;
            _aclService = aclService;
            _compareProductsService = compareProductsService;
            _customerActivityService = customerActivityService;
            _customerService = customerService;
            _eventPublisher = eventPublisher;
            _htmlFormatter = htmlFormatter;
            _localizationService = localizationService;
            _orderService = orderService;
            _permissionService = permissionService;
            _productAttributeParser = productAttributeParser;
            _productModelFactory = productModelFactory;
            _productService = productService;
            _reviewTypeService = reviewTypeService;
            _recentlyViewedProductsService = recentlyViewedProductsService;
            _shoppingCartModelFactory = shoppingCartModelFactory;
            _shoppingCartService = shoppingCartService;
            _storeContext = storeContext;
            _storeMappingService = storeMappingService;
            _urlRecordService = urlRecordService;
            _webHelper = webHelper;
            _workContext = workContext;
            _workflowMessageService = workflowMessageService;
            _localizationSettings = localizationSettings;
            _shoppingCartSettings = shoppingCartSettings;
            _shippingSettings = shippingSettings;
            _customProductReviewMappingService = customProductReviewMappingService;
            _queue = queue;
            _customProductReviewsSettings = customProductReviewsSettings;

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
        [HttpPost]
        [ValidateCaptcha]
        [RequestFormLimits(MultipartBodyLengthLimit = 1048576000)]
        [RequestSizeLimit(1048576000)]
        public virtual async Task<IActionResult> ProductReviewsAdd(int productId, ProductReviewsModel model, bool captchaValid, List<IFormFile> photos)
        {

            var product = await _productService.GetProductByIdAsync(productId);
            var currentStore = await _storeContext.GetCurrentStoreAsync();

            if (product == null || product.Deleted || !product.Published || !product.AllowCustomerReviews ||
                !await _productService.CanAddReviewAsync(product.Id,
                    _catalogSettings.ShowProductReviewsPerStore ? currentStore.Id : 0))
                return RedirectToRoute("Homepage");

            //validate CAPTCHA
            if (_captchaSettings.Enabled && _captchaSettings.ShowOnProductReviewPage && !captchaValid)
            {
                ModelState.AddModelError("", await _localizationService.GetResourceAsync("Common.WrongCaptchaMessage"));
            }

            await ValidateProductReviewAvailabilityAsync(product);
            var mediaFiles = photos?.Where(file => file?.Length > 0).ToList() ?? new List<IFormFile>();
            ValidateReviewMediaUploads(mediaFiles);

            if (ModelState.IsValid)
            {
                //save review
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
                await _productService.InsertProductReviewAsync(productReview);
                var reviewId = productReview.Id;




                //add product review and review type mapping                
                foreach (var additionalReview in model.AddAdditionalProductReviewList)
                {
                    var additionalProductReview = new ProductReviewReviewTypeMapping { ProductReviewId = productReview.Id, ReviewTypeId = additionalReview.ReviewTypeId, Rating = additionalReview.Rating };

                    await _reviewTypeService.InsertProductReviewReviewTypeMappingsAsync(additionalProductReview);
                }

                //update product totals
                await _productService.UpdateProductReviewTotalsAsync(product);

                //notify store owner
                if (_catalogSettings.NotifyStoreOwnerAboutNewProductReviews)
                    await _workflowMessageService.SendProductReviewStoreOwnerNotificationMessageAsync(productReview,
                        _localizationSettings.DefaultAdminLanguageId);

                //activity log
                await _customerActivityService.InsertActivityAsync("PublicStore.AddProductReview",
                    string.Format(
                        await _localizationService.GetResourceAsync("ActivityLog.PublicStore.AddProductReview"),
                        product.Name), product);

                //raise event
                if (productReview.IsApproved)
                    await _eventPublisher.PublishAsync(new ProductReviewApprovedEvent(productReview));

                model = await _productModelFactory.PrepareProductReviewsModelAsync(product);
                model.AddProductReview.Title = null;
                model.AddProductReview.ReviewText = null;

                #region Product Review Media Upload Section

                // Input was validated before saving the review. The worker logs
                // any decode/transcode failure instead of silently dropping it.
                List<UploadDataBinary> dataList = new List<UploadDataBinary>();


                foreach (var photo in mediaFiles)
                {
                    var uploadData = new UploadDataBinary();

                    uploadData.Extentions = GetCanonicalMediaMimeType(photo);
                        
                  
                    using (var ms = new MemoryStream())
                    {
                        await photo.CopyToAsync(ms);
                        uploadData.BinaryData = ms.ToArray();
                        dataList.Add(uploadData);
                    }
                        
                        
                    //string fileName = "tempUpload"+DateTime.UtcNow.ToFileTime() + fileInfo.Extension;


                   
                }

                foreach (var data in dataList)
                {
                   _queue.QueueTask(async token =>
                    {
                       await InsertReviewMedia(model, data, reviewId);
                    });
                }

                #endregion
                var result = !isApproved
                    ? await _localizationService.GetResourceAsync("Reviews.SeeAfterApproving")
                    : await _localizationService.GetResourceAsync("Reviews.SuccessfullyAdded");
                result += Environment.NewLine + " Your uploaded media(photo or video ) will continue to be processed in the background." + Environment.NewLine +
                    " After processing, the media will be automatically added to your review.";
                return Json(new { Model = model, Success = true, Result = result });

            }
            //if we got this far, something failed, redisplay form
            model = await _productModelFactory.PrepareProductReviewsModelAsync(product);
            var errors = ModelState.Values
                .SelectMany(state => state.Errors)
                .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage) ? "The review could not be submitted." : error.ErrorMessage)
                .Distinct()
                .ToList();
            return Json(new { Model = model, Success = false, Result = string.Join(" ", errors) });
        }
    

        private void ValidateReviewMediaUploads(IReadOnlyCollection<IFormFile> mediaFiles)
        {
            if (mediaFiles.Count > _customProductReviewsSettings.MaximumFile)
                ModelState.AddModelError("photos", "Too many review-media files were selected.");

            foreach (var file in mediaFiles)
            {
                var mimeType = GetCanonicalMediaMimeType(file);
                if (string.IsNullOrWhiteSpace(mimeType))
                {
                    ModelState.AddModelError("photos", "Unsupported review-media type. Use JPG, PNG, WebP, MP4, MOV or WebM.");
                    continue;
                }

                var maximumSize = mimeType.StartsWith("video/", StringComparison.OrdinalIgnoreCase)
                    ? Math.Clamp(_customProductReviewsSettings.MaximumVideoSizeBytes, 5 * 1024 * 1024, 250 * 1024 * 1024)
                    : _customProductReviewsSettings.MaximumSize;
                if (maximumSize > 0 && file.Length > maximumSize)
                    ModelState.AddModelError("photos", "A review-media file exceeds the allowed file size.");
            }
        }

        private static string GetCanonicalMediaMimeType(IFormFile file)
        {
            var contentType = file?.ContentType?.Trim().ToLowerInvariant();
            var extension = Path.GetExtension(file?.FileName ?? string.Empty).ToLowerInvariant();
            return (contentType, extension) switch
            {
                ("image/jpeg", _) or ("image/jpg", _) or (_, ".jpg") or (_, ".jpeg") => "image/jpeg",
                ("image/png", _) or (_, ".png") => "image/png",
                ("image/webp", _) or (_, ".webp") => "image/webp",
                ("video/mp4", _) or ("video/mpeg4", _) or (_, ".mp4") => Nop.Plugin.Widgets.CustomProductReviews.Data.MimeTypes.VideoMp4,
                ("video/quicktime", _) or ("video/mov", _) or (_, ".mov") => Nop.Plugin.Widgets.CustomProductReviews.Data.MimeTypes.VideoMov,
                ("video/webm", _) or (_, ".webm") => Nop.Plugin.Widgets.CustomProductReviews.Data.MimeTypes.VideoWebm,
                _ => string.Empty
            };
        }

    public async Task<string> InsertReviewMedia(ProductReviewsModel model, UploadDataBinary data, int reviewId)
        {

            var reviewProduct = await _productService.GetProductByIdAsync(model.ProductId);
            var productSeName = reviewProduct == null ? model.ProductId.ToString() : await _urlRecordService.GetSeNameAsync(reviewProduct);
            string name = productSeName + "-" + DateTime.UtcNow.ToFileTime();
            Stopwatch sw = new Stopwatch();

            var pic = new Picture();


                Video vid = new Video();
                if (data.Extentions.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    sw.Start();

                    using var bitmap = SKBitmap.Decode(data.BinaryData)
                        ?? throw new InvalidDataException("The uploaded review image could not be decoded.");
                    using var image = SKImage.FromBitmap(bitmap);
                    using var encoded = image.Encode(SKEncodedImageFormat.Webp, 90)
                        ?? throw new InvalidDataException("The uploaded review image could not be encoded as WebP.");
                    sw.Stop();

                    var raw = encoded.ToArray();
                    pic = await _pictureService.InsertPictureAsync(raw, "image/webp", name);
                }
                else if (data.Extentions.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
                {
                    vid = await _videoService.InsertVideoAsync(data.BinaryData, name, data.Extentions);
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
                    await _customProductReviewMappingService.InsertCustomProductReviewMappingAsync(reviewId, lastPicId,
                        lastVidId);
                }
           
            return "done";
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
