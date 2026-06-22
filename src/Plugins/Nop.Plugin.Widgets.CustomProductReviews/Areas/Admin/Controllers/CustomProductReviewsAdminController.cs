using Microsoft.AspNetCore.Mvc;
using Nop.Core.Domain.Media;
using Nop.Data;
using Nop.Plugin.Widgets.CustomProductReviews.Areas.Admin.Models;
using Nop.Plugin.Widgets.CustomProductReviews.Domains;
using Nop.Plugin.Widgets.CustomProductReviews.Services;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Media;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nop.Plugin.Widgets.CustomProductReviews.Areas.Admin.Controllers
{
    [Area(AreaNames.ADMIN)]
    [AuthorizeAdmin]
    [Route("Admin/CustomProductReviewsAdmin/[action]")]
    public class CustomProductReviewsAdminController : BasePluginController
    {
        private readonly ISettingService _settingService;
        private readonly ILocalizationService _localizationService;
        private readonly IPermissionService _permissionService;
        private readonly INotificationService _notificationService;
        private readonly IPictureService _pictureService;
        private readonly IProductReviewVideoService _videoService;
        private readonly ICustomProductReviewMappingService _customProductReviewMappingService;
        private readonly IRepository<CustomProductReviewMapping> _customProductReviewMappingRepository;

        public CustomProductReviewsAdminController(
            ISettingService settingService,
            ILocalizationService localizationService,
            IPermissionService permissionService,
            INotificationService notificationService,
            IPictureService pictureService,
            IProductReviewVideoService videoService,
            ICustomProductReviewMappingService customProductReviewMappingService,
            IRepository<CustomProductReviewMapping> customProductReviewMappingRepository)
        {
            _settingService = settingService;
            _localizationService = localizationService;
            _permissionService = permissionService;
            _notificationService = notificationService;
            _pictureService = pictureService;
            _videoService = videoService;
            _customProductReviewMappingService = customProductReviewMappingService;
            _customProductReviewMappingRepository = customProductReviewMappingRepository;
        }

        [HttpGet]
        public virtual async Task<IActionResult> Configure()
        {
            // Access is restricted by [AuthorizeAdmin].
            // Avoid StandardPermissionProvider here because nopCommerce 4.80 changed/removed this static provider in some builds.

            var settings = await _settingService.LoadSettingAsync<CustomProductReviewsSettings>();

            var model = new ConfigurationModel
            {
                WidgetZone = settings.WidgetZone,
                MaximumFile = settings.MaximumFile <= 0 ? 5 : settings.MaximumFile,
                MaximumSize = settings.MaximumSize <= 0 ? 1073741824 : settings.MaximumSize,
                AdminShowMediaOnProductReviewList = settings.AdminShowMediaOnProductReviewList,
                AdminMediaThumbSize = settings.AdminMediaThumbSize <= 0 ? 72 : settings.AdminMediaThumbSize,
                AdminMediaMaxItemsPerReview = settings.AdminMediaMaxItemsPerReview <= 0 ? 6 : settings.AdminMediaMaxItemsPerReview,
                PublicCompactReviewLayout = settings.PublicCompactReviewLayout
            };

            return View("~/Plugins/Widgets.CustomProductReviews/Areas/Admin/Views/CustomProductReviewsAdmin/Configure.cshtml", model);
        }

        [HttpPost]
        public virtual async Task<IActionResult> Configure(ConfigurationModel model)
        {
            // Access is restricted by [AuthorizeAdmin].
            // Avoid StandardPermissionProvider here because nopCommerce 4.80 changed/removed this static provider in some builds.

            if (model.MaximumFile < 0)
                model.MaximumFile = 0;

            if (model.MaximumSize < 0)
                model.MaximumSize = 0;

            if (model.AdminMediaThumbSize <= 0)
                model.AdminMediaThumbSize = 72;

            if (model.AdminMediaMaxItemsPerReview <= 0)
                model.AdminMediaMaxItemsPerReview = 6;

            await _settingService.SaveSettingAsync(new CustomProductReviewsSettings
            {
                WidgetZone = model.WidgetZone,
                MaximumFile = model.MaximumFile,
                MaximumSize = model.MaximumSize,
                AdminShowMediaOnProductReviewList = model.AdminShowMediaOnProductReviewList,
                AdminMediaThumbSize = model.AdminMediaThumbSize,
                AdminMediaMaxItemsPerReview = model.AdminMediaMaxItemsPerReview,
                PublicCompactReviewLayout = model.PublicCompactReviewLayout
            });

            _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));
            return await Configure();
        }

        [HttpGet]
        public virtual async Task<IActionResult> ReviewMediaSummary(string productReviewIds)
        {
            // Access is restricted by [AuthorizeAdmin].
            // This endpoint is used only inside admin product-review list.

            var settings = await _settingService.LoadSettingAsync<CustomProductReviewsSettings>();
            if (!settings.AdminShowMediaOnProductReviewList)
                return Json(new { success = true, items = new object[0] });

            var ids = ParseIds(productReviewIds).Take(100).ToList();
            if (!ids.Any())
                return Json(new { success = true, items = new object[0] });

            var maxItems = settings.AdminMediaMaxItemsPerReview <= 0 ? 6 : settings.AdminMediaMaxItemsPerReview;
            var thumbSize = settings.AdminMediaThumbSize <= 0 ? 72 : settings.AdminMediaThumbSize;
            var response = new List<object>();

            foreach (var reviewId in ids)
            {
                var mappings = await _customProductReviewMappingService.GetCustomProductReviewMappingByProductReviewIdAsync(reviewId);
                var media = new List<object>();

                foreach (var mapping in mappings.Where(x => x.PictureId.HasValue || x.ProductReviewVideoId.HasValue).Take(maxItems))
                {
                    if (mapping.PictureId.HasValue && mapping.PictureId.Value > 0)
                    {
                        var thumbUrl = await _pictureService.GetPictureUrlAsync(mapping.PictureId.Value, thumbSize, true);
                        var fullUrl = await _pictureService.GetPictureUrlAsync(mapping.PictureId.Value, 0, true);
                        media.Add(new
                        {
                            type = "image",
                            pictureId = mapping.PictureId.Value,
                            url = fullUrl,
                            thumbUrl
                        });
                    }

                    if (mapping.ProductReviewVideoId.HasValue && mapping.ProductReviewVideoId.Value > 0)
                    {
                        var videoUrl = await _videoService.GetVideoUrlAsync(mapping.ProductReviewVideoId.Value, 0, true);
                        media.Add(new
                        {
                            type = "video",
                            videoId = mapping.ProductReviewVideoId.Value,
                            url = videoUrl,
                            thumbUrl = string.Empty
                        });
                    }
                }

                response.Add(new
                {
                    productReviewId = reviewId,
                    count = media.Count,
                    media
                });
            }

            return Json(new { success = true, items = response });
        }

        [HttpGet]
        public virtual async Task<IActionResult> Diagnostics()
        {
            // Access is restricted by [AuthorizeAdmin].
            // Avoid StandardPermissionProvider here because nopCommerce 4.80 changed/removed this static provider in some builds.

            var settings = await _settingService.LoadSettingAsync<CustomProductReviewsSettings>();
            var totalMappings = _customProductReviewMappingRepository.Table.Count();

            return Json(new
            {
                success = true,
                settings,
                totalMappings
            });
        }

        private static IEnumerable<int> ParseIds(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                yield break;

            var parts = value.Split(new[] { ',', ';', ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                if (int.TryParse(part, out var id) && id > 0)
                    yield return id;
            }
        }
    }
}
