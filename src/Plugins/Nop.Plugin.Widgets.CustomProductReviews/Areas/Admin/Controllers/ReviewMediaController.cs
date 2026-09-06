using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Core.Domain.Catalog;
using Nop.Data;
using Nop.Plugin.Widgets.CustomProductReviews.Areas.Admin.Models;
using Nop.Plugin.Widgets.CustomProductReviews.Services;
using Nop.Services.Catalog;
using Nop.Services.Configuration;
using Nop.Services.Media;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;
using ReviewVideoService = Nop.Plugin.Widgets.CustomProductReviews.Services.IVideoService;

namespace Nop.Plugin.Widgets.CustomProductReviews.Areas.Admin.Controllers;

[AuthorizeAdmin]
[Area(AreaNames.ADMIN)]
[AutoValidateAntiforgeryToken]
public sealed class ReviewMediaController : BasePluginController
{
    private readonly ICustomProductReviewMappingService _mappingService;
    private readonly IRepository<ProductReview> _productReviewRepository;
    private readonly IProductService _productService;
    private readonly IPictureService _pictureService;
    private readonly ReviewVideoService _videoService;
    private readonly ISettingService _settingService;
    private readonly CustomProductReviewsSettings _settings;

    public ReviewMediaController(
        ICustomProductReviewMappingService mappingService,
        IRepository<ProductReview> productReviewRepository,
        IProductService productService,
        IPictureService pictureService,
        ReviewVideoService videoService,
        ISettingService settingService,
        CustomProductReviewsSettings settings)
    {
        _mappingService = mappingService;
        _productReviewRepository = productReviewRepository;
        _productService = productService;
        _pictureService = pictureService;
        _videoService = videoService;
        _settingService = settingService;
        _settings = settings;
    }

    [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
    public async Task<IActionResult> Manage(int reviewId = 0)
    {
        var mappings = await _mappingService.GetCustomProductReviewMappingsAsync(reviewId, pageSize: 100);
        var items = new List<ReviewMediaItemModel>();
        foreach (var mapping in mappings)
        {
            var review = await _productReviewRepository.GetByIdAsync(mapping.ProductReviewId, cache => default);
            if (review == null)
                continue;

            var product = await _productService.GetProductByIdAsync(review.ProductId);
            if (mapping.PictureId is > 0)
            {
                var picture = await _pictureService.GetPictureByIdAsync(mapping.PictureId.Value);
                if (picture == null)
                    continue;
                var (url, _) = await _pictureService.GetPictureUrlAsync(picture, 200, false);
                items.Add(new ReviewMediaItemModel
                {
                    Id = mapping.Id,
                    ReviewId = mapping.ProductReviewId,
                    ProductId = review.ProductId,
                    ProductName = product?.Name ?? $"Product #{review.ProductId}",
                    ReviewTitle = review.Title,
                    IsVideo = false,
                    MediaUrl = url,
                    MimeType = picture.MimeType,
                    DisplayOrder = mapping.DisplayOrder,
                    AltAttribute = picture.AltAttribute,
                    TitleAttribute = picture.TitleAttribute
                });
            }
            else if (mapping.ProductReviewVideoId is > 0)
            {
                var video = await _videoService.GetVideoByIdAsync(mapping.ProductReviewVideoId.Value);
                if (video == null)
                    continue;
                var (url, _) = await _videoService.GetVideoUrlAsync(video, showDefaultVideo: false);
                items.Add(new ReviewMediaItemModel
                {
                    Id = mapping.Id,
                    ReviewId = mapping.ProductReviewId,
                    ProductId = review.ProductId,
                    ProductName = product?.Name ?? $"Product #{review.ProductId}",
                    ReviewTitle = review.Title,
                    IsVideo = true,
                    MediaUrl = url,
                    MimeType = video.MimeType,
                    DisplayOrder = mapping.DisplayOrder,
                    AltAttribute = video.AltAttribute,
                    TitleAttribute = video.TitleAttribute
                });
            }
        }

        return View("~/Plugins/Widgets.CustomProductReviews/Areas/Admin/Views/ReviewMedia/Manage.cshtml", new ReviewMediaManageModel
        {
            ReviewId = reviewId,
            Items = items,
            Settings = _settings
        });
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
    public async Task<IActionResult> Update(ReviewMediaEditModel model)
    {
        var mapping = await _mappingService.GetCustomProductReviewMappingByIdAsync(model.Id);
        if (mapping == null)
            return NotFound();

        mapping.DisplayOrder = model.DisplayOrder;
        await _mappingService.UpdateCustomProductReviewMappingAsync(mapping);
        if (mapping.PictureId is > 0)
        {
            var picture = await _pictureService.GetPictureByIdAsync(mapping.PictureId.Value);
            if (picture != null)
            {
                picture.AltAttribute = model.AltAttribute;
                picture.TitleAttribute = model.TitleAttribute;
                await _pictureService.UpdatePictureAsync(picture);
            }
        }
        else if (mapping.ProductReviewVideoId is > 0)
        {
            var video = await _videoService.GetVideoByIdAsync(mapping.ProductReviewVideoId.Value);
            if (video != null)
            {
                video.AltAttribute = model.AltAttribute;
                video.TitleAttribute = model.TitleAttribute;
                await _videoService.UpdateVideoAsync(video);
            }
        }

        return RedirectToAction(nameof(Manage), new { reviewId = model.ReviewId });
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
    public async Task<IActionResult> Delete(int id, int reviewId)
    {
        var mapping = await _mappingService.GetCustomProductReviewMappingByIdAsync(id);
        if (mapping == null)
            return NotFound();

        var pictureId = mapping.PictureId;
        var videoId = mapping.ProductReviewVideoId;
        await _mappingService.DeleteCustomProductReviewMappingAsync(mapping);

        // A media asset is removed only after its final review mapping is gone.
        if (pictureId is > 0 && await _mappingService.GetPictureUsageCountAsync(pictureId.Value) == 0)
        {
            var picture = await _pictureService.GetPictureByIdAsync(pictureId.Value);
            if (picture != null)
                await _pictureService.DeletePictureAsync(picture);
        }
        if (videoId is > 0 && await _mappingService.GetVideoUsageCountAsync(videoId.Value) == 0)
        {
            var video = await _videoService.GetVideoByIdAsync(videoId.Value);
            if (video != null)
                await _videoService.DeleteVideoAsync(video);
        }

        return RedirectToAction(nameof(Manage), new { reviewId });
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
    public async Task<IActionResult> SaveSettings(ReviewMediaSettingsModel model, int reviewId = 0)
    {
        _settings.EnableReviewVideoTranscoding = model.EnableReviewVideoTranscoding;
        _settings.FfmpegExecutablePath = model.FfmpegExecutablePath?.Trim();
        _settings.FfprobeExecutablePath = model.FfprobeExecutablePath?.Trim();
        _settings.MaximumVideoSizeBytes = Math.Clamp(model.MaximumVideoSizeBytes, 5 * 1024 * 1024, 250 * 1024 * 1024);
        _settings.MaximumVideoDurationSeconds = model.MaximumVideoDurationSeconds;
        _settings.MaximumVideoWidth = model.MaximumVideoWidth;
        _settings.MaximumVideoHeight = model.MaximumVideoHeight;
        _settings.NormalizedVideoMaxWidth = model.NormalizedVideoMaxWidth;
        _settings.VideoCrf = model.VideoCrf;
        _settings.VideoTranscodeTimeoutSeconds = model.VideoTranscodeTimeoutSeconds;
        await _settingService.SaveSettingAsync(_settings);

        return RedirectToAction(nameof(Manage), new { reviewId });
    }
}
