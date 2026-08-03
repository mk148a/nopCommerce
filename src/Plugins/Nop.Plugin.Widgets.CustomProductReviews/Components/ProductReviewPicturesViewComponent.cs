using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Html;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Drawing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Nop.Core.Caching;
using Nop.Plugin.Widgets.CustomProductReviews.Services;
using Nop.Web.Framework.Components;
using Nop.Web.Models.Catalog;
using Nop.Services.Catalog;
using Nop.Web.Factories;
using Nop.Services.Media;
using Picture = Nop.Core.Domain.Media.Picture;
using Nop.Web.Models.Media;
using Nop.Services.Localization;
using Nop.Core.Domain.Media;
using DocumentFormat.OpenXml.Bibliography;

namespace Nop.Plugin.Widgets.CustomProductReviews.Components
{
    [ViewComponent(Name = "ProductReviewPictures")]
    public class ProductReviewPictures : NopViewComponent
    {

        #region Fields

        //private readonly CustomProductReviewsService _customProductReviewsServiceService;
        private readonly CustomProductReviewsSettings _customProductReviewsSettings;
        private readonly IProductService _productService;
        private readonly IProductModelFactory _productModelFactory;
        private readonly IPictureService _pictureService;
        private readonly ICustomProductReviewMappingService _customProductReviewMappingService;
        private readonly ILocalizationService _localizationService;
        private readonly MediaSettings _mediaSettings;
        private readonly IShortTermCacheManager _shortTermCacheManager;

        private static readonly CacheKey ReviewPictureModelsCacheKey =
            new("Nop.Plugin.Widgets.CustomProductReviews.ReviewPictureModels.{0}");



        #endregion

        #region Ctor

        public ProductReviewPictures(CustomProductReviewsSettings customProductReviewsSettings, IProductService productService, IProductModelFactory productModelFactory,IPictureService pictureService, ICustomProductReviewMappingService customProductReviewMappingService, ILocalizationService localizationService, MediaSettings mediaSettings, IShortTermCacheManager shortTermCacheManager)
        {
            //_accessiBeService = accessiBeService;
            _customProductReviewsSettings = customProductReviewsSettings;
            _productService = productService;
            _productModelFactory = productModelFactory;
            _pictureService = pictureService;
            _customProductReviewMappingService = customProductReviewMappingService;
            _localizationService=localizationService;
            _mediaSettings = mediaSettings;
            _shortTermCacheManager = shortTermCacheManager;
        }

    

        #endregion

        #region Methods

        /// <summary>
        /// Invoke view component
        /// </summary>
        /// <param name="widgetZone">Widget zone name</param>
        /// <param name="additionalData">Additional data</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the view component result
        /// </returns>
        public async Task<IViewComponentResult> InvokeAsync(string widgetZone, object additionalData)
        {
            //var model = new ProductReviewsModel();
            //var productDetailModel = new ProductDetailsModel();
            //if (additionalData.GetType() == model.GetType())
            //{
            //    model = (ProductReviewsModel)additionalData;
            //}
            //else
            //{
            //    productDetailModel = (ProductDetailsModel)additionalData;
            //    int productId = productDetailModel.Id;
            //    var product = await _productService.GetProductByIdAsync(productId);
            //    model = await _productModelFactory.PrepareProductReviewsModelAsync(new ProductReviewsModel(), product);
            //}

            //return View("~/Plugins/Widgets.CustomProductReviews/Views/ProductReviewComponent.cshtml", model);

            var model = new ProductReviewModel();
            if (additionalData.GetType() == model.GetType())
            {
                model = (ProductReviewModel)additionalData;
            }
            if (model.Id <= 0)
                return View("~/Plugins/Widgets.CustomProductReviews/Views/_ProductReviewPictures.cshtml", new List<PictureModel>());

            var pictureModelList = await _shortTermCacheManager.GetAsync(
                async () => await PreparePictureModelsAsync(model),
                ReviewPictureModelsCacheKey,
                model.Id);

            return View("~/Plugins/Widgets.CustomProductReviews/Views/_ProductReviewPictures.cshtml", pictureModelList ?? new List<PictureModel>());

        }

        private async Task<List<PictureModel>> PreparePictureModelsAsync(ProductReviewModel model)
        {
            var reviewMappings = await _customProductReviewMappingService
                .GetCustomProductReviewMappingByProductReviewIdAsync(model.Id);
            if (reviewMappings == null || reviewMappings.Count == 0)
                return new List<PictureModel>();

            const int thumbnailSize = 150;
            var defaultTitle = string.Format(
                await _localizationService.GetResourceAsync("Media.Product.ImageLinkTitleFormat.Details"), model.Title);
            var defaultAlt = string.Format(
                await _localizationService.GetResourceAsync("Media.Product.ImageAlternateTextFormat.Details"), model.Title);
            var pictureModels = new List<PictureModel>();

            foreach (var mapping in reviewMappings.OrderBy(mapping => mapping.DisplayOrder).ThenBy(mapping => mapping.Id))
            {
                if (mapping.PictureId is not > 0)
                    continue;

                var picture = await _pictureService.GetPictureByIdAsync(mapping.PictureId.Value);
                if (picture == null)
                    continue;

                var (imageUrl, pictureWithUrl) = await _pictureService.GetPictureUrlAsync(picture, thumbnailSize, true);
                var (fullSizeImageUrl, _) = await _pictureService.GetPictureUrlAsync(pictureWithUrl);
                if (string.IsNullOrWhiteSpace(imageUrl) || string.IsNullOrWhiteSpace(fullSizeImageUrl))
                    continue;

                pictureModels.Add(new PictureModel
                {
                    ImageUrl = imageUrl,
                    FullSizeImageUrl = fullSizeImageUrl,
                    Title = string.IsNullOrWhiteSpace(picture.TitleAttribute) ? defaultTitle : picture.TitleAttribute,
                    AlternateText = string.IsNullOrWhiteSpace(picture.AltAttribute) ? defaultAlt : picture.AltAttribute
                });
            }

            return pictureModels;
        }

        #endregion
    }
}
