using System;
using System.Collections.Generic;

namespace Nop.Plugin.Widgets.CustomProductReviews
{
    /// <summary>
    /// Default locale resources for the Custom Product Reviews plugin.
    /// Kept in one place so install and admin configure can repair missing resources.
    /// nopCommerce internally looks up many resources in lower-case, so plugin keys are saved twice:
    /// original casing and lower-case.
    /// </summary>
    public static class CustomProductReviewsLocaleResources
    {
        public static Dictionary<string, string> GetDefaultResources()
        {
            var resources = new Dictionary<string, string>();

            Add(resources, "Plugins.Widgets.CustomProductReviews.Configuration", "Custom product reviews settings");
            Add(resources, "Plugins.Widgets.CustomProductReviews.Configuration.AdminMedia", "Admin product review list media");
            Add(resources, "Plugins.Widgets.CustomProductReviews.Configuration.UploadHint", "Default/current large upload setting: 1073741824 bytes ≈ 1 GB.");

            Add(resources, "Plugins.Widgets.CustomProductReviews.Fields.Enabled", "Enable");
            Add(resources, "Plugins.Widgets.CustomProductReviews.Fields.Enabled.Hint", "Check to activate this widget.");
            Add(resources, "Plugins.Widgets.CustomProductReviews.Fields.Script", "Installation script");
            Add(resources, "Plugins.Widgets.CustomProductReviews.Fields.Script.Hint", "Find your unique installation script on the Installation tab in your account and then copy it into this field.");
            Add(resources, "Plugins.Widgets.CustomProductReviews.Fields.Script.Required", "Installation script is required");

            Add(resources, "Plugins.Widgets.CustomProductReviews.Fields.WidgetZone", "Widget zone");
            Add(resources, "Plugins.Widgets.CustomProductReviews.Fields.WidgetZone.Hint", "Widget zone where the custom product review widget is rendered.");
            Add(resources, "Plugins.Widgets.CustomProductReviews.Fields.MaximumFile", "Maximum files per review");
            Add(resources, "Plugins.Widgets.CustomProductReviews.Fields.MaximumFile.Hint", "Maximum number of uploaded media files accepted for one product review.");
            Add(resources, "Plugins.Widgets.CustomProductReviews.Fields.MaximumSize", "Maximum upload size (bytes)");
            Add(resources, "Plugins.Widgets.CustomProductReviews.Fields.MaximumSize.Hint", "Maximum file size allowed for each uploaded review photo or video, in bytes.");
            Add(resources, "Plugins.Widgets.CustomProductReviews.Fields.AdminShowMediaOnProductReviewList", "Show media in admin product review list");
            Add(resources, "Plugins.Widgets.CustomProductReviews.Fields.AdminShowMediaOnProductReviewList.Hint", "Show product review photo/video thumbnails on Admin > Catalog > Product reviews.");
            Add(resources, "Plugins.Widgets.CustomProductReviews.Fields.AdminMediaThumbSize", "Admin media thumbnail size (px)");
            Add(resources, "Plugins.Widgets.CustomProductReviews.Fields.AdminMediaThumbSize.Hint", "Thumbnail width/height in pixels for admin product review media previews.");
            Add(resources, "Plugins.Widgets.CustomProductReviews.Fields.AdminMediaMaxItemsPerReview", "Max media items per review row");
            Add(resources, "Plugins.Widgets.CustomProductReviews.Fields.AdminMediaMaxItemsPerReview.Hint", "Maximum number of photos/videos shown in one admin product review row.");
            Add(resources, "Plugins.Widgets.CustomProductReviews.Fields.PublicCompactReviewLayout", "Use compact public review layout");
            Add(resources, "Plugins.Widgets.CustomProductReviews.Fields.PublicCompactReviewLayout.Hint", "Reduce empty space in public product review layout.");

            Add(resources, "Plugins.Widgets.CustomProductReviews.ProductReviewsFor", "Product reviews for");
            Add(resources, "Product Reviews For", "Product reviews for");
            Add(resources, "Product Reviews For ", "Product reviews for");
            Add(resources, "product reviews for", "Product reviews for");
            Add(resources, "Plugins.Widgets.CustomProductReviews.AttachFiles", "Attach files");
            Add(resources, "Plugins.Widgets.CustomProductReviews.MaxFilesInUpload", "Maximum files in upload: {0}");
            Add(resources, "Plugins.Widgets.CustomProductReviews.MediaProcessingStarted", "Your uploaded media (photo or video) will continue to be processed in the background.");
            Add(resources, "Plugins.Widgets.CustomProductReviews.MediaProcessingCompletedAutomatically", "After processing, the media will be automatically added to your review.");
            Add(resources, "Plugins.Widgets.CustomProductReviews.UnsupportedFileFormat", "File format is not supported for upload.");
            Add(resources, "Plugins.Widgets.CustomProductReviews.UploadFileFormatError", "Upload file format error.");
            Add(resources, "Plugins.Widgets.CustomProductReviews.GeneralError", "A general error occurred. Please try again.");
            Add(resources, "Plugins.Widgets.CustomProductReviews.ViewLargerReviewPhoto", "View larger review photo");
            Add(resources, "Plugins.Widgets.CustomProductReviews.HoverToZoom", "Hover to enlarge");
            Add(resources, "Plugins.Widgets.CustomProductReviews.VideoNotSupported", "Your browser does not support the video tag.");
            Add(resources, "Plugins.Widgets.CustomProductReviews.ReviewVideo", "Review video");
            Add(resources, "Plugins.Widgets.CustomProductReviews.AdminMedia", "Review media");
            Add(resources, "Plugins.Widgets.CustomProductReviews.NoAdminMedia", "No media");

            return resources;
        }

        private static void Add(IDictionary<string, string> resources, string key, string value)
        {
            resources[key] = value;

            if (key.StartsWith("Plugins.", StringComparison.Ordinal))
                resources[key.ToLowerInvariant()] = value;
        }
    }
}
