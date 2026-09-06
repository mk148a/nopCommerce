using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.Widgets.CustomProductReviews.Areas.Admin.Models;
using Nop.Plugin.Widgets.CustomProductReviews.Services;
using Nop.Services.Media;
using Nop.Web.Framework.Components;
using Nop.Web.Framework.Infrastructure;
using AdminProductReviewModel = Nop.Web.Areas.Admin.Models.Catalog.ProductReviewModel;
using ReviewVideoService = Nop.Plugin.Widgets.CustomProductReviews.Services.IVideoService;

namespace Nop.Plugin.Widgets.CustomProductReviews.Components;

[ViewComponent(Name = "AdminReviewMediaWidget")]
public sealed class AdminReviewMediaWidgetViewComponent : NopViewComponent
{
    private readonly ICustomProductReviewMappingService _mappingService;
    private readonly IPictureService _pictureService;
    private readonly ReviewVideoService _videoService;

    public AdminReviewMediaWidgetViewComponent(
        ICustomProductReviewMappingService mappingService,
        IPictureService pictureService,
        ReviewVideoService videoService)
    {
        _mappingService = mappingService;
        _pictureService = pictureService;
        _videoService = videoService;
    }

    public async Task<IViewComponentResult> InvokeAsync(string widgetZone, object additionalData)
    {
        if (widgetZone == AdminWidgetZones.ProductReviewListButtons)
            return View("~/Plugins/Widgets.CustomProductReviews/Areas/Admin/Views/ReviewMedia/_ReviewMediaListButton.cshtml");

        if (additionalData is not AdminProductReviewModel review || review.Id <= 0)
            return Content(string.Empty);

        var model = new ReviewMediaWidgetModel { ReviewId = review.Id };
        var mappings = await _mappingService.GetCustomProductReviewMappingsAsync(review.Id, pageSize: 6);
        foreach (var mapping in mappings)
        {
            if (mapping.PictureId is > 0)
            {
                var picture = await _pictureService.GetPictureByIdAsync(mapping.PictureId.Value);
                if (picture == null)
                    continue;

                var (url, _) = await _pictureService.GetPictureUrlAsync(picture, 96, false);
                model.Items.Add(new ReviewMediaItemModel
                {
                    Id = mapping.Id,
                    ReviewId = review.Id,
                    IsVideo = false,
                    MediaUrl = url,
                    MimeType = picture.MimeType,
                    AltAttribute = picture.AltAttribute,
                    TitleAttribute = picture.TitleAttribute
                });
                continue;
            }

            if (mapping.ProductReviewVideoId is not > 0)
                continue;

            var video = await _videoService.GetVideoByIdAsync(mapping.ProductReviewVideoId.Value);
            if (video == null)
                continue;

            try
            {
                var (url, _) = await _videoService.GetVideoUrlAsync(video, showDefaultVideo: false);
                if (string.IsNullOrWhiteSpace(url))
                    continue;

                model.Items.Add(new ReviewMediaItemModel
                {
                    Id = mapping.Id,
                    ReviewId = review.Id,
                    IsVideo = true,
                    MediaUrl = url,
                    MimeType = video.MimeType,
                    AltAttribute = video.AltAttribute,
                    TitleAttribute = video.TitleAttribute
                });
            }
            catch (InvalidOperationException)
            {
                // Keep the core review editor available even if a historical video is corrupt.
            }
        }

        return View("~/Plugins/Widgets.CustomProductReviews/Areas/Admin/Views/ReviewMedia/_ReviewMediaWidget.cshtml", model);
    }
}
