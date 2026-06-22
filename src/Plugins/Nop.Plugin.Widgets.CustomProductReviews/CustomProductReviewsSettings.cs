using Nop.Core.Configuration;

namespace Nop.Plugin.Widgets.CustomProductReviews
{
    /// <summary>
    /// Represents Custom Product Reviews plugin settings.
    /// </summary>
    public class CustomProductReviewsSettings : ISettings
    {
        public string WidgetZone { get; set; }

        /// <summary>
        /// Maximum number of uploaded media files per review.
        /// </summary>
        public int MaximumFile { get; set; }

        /// <summary>
        /// Maximum uploaded file size in bytes.
        /// </summary>
        public int MaximumSize { get; set; }

        /// <summary>
        /// Enable admin product review list media thumbnails/videos injection.
        /// </summary>
        public bool AdminShowMediaOnProductReviewList { get; set; }

        /// <summary>
        /// Thumbnail size in pixels shown in admin product review list.
        /// </summary>
        public int AdminMediaThumbSize { get; set; }

        /// <summary>
        /// Max media items shown per review row in admin product review list.
        /// </summary>
        public int AdminMediaMaxItemsPerReview { get; set; }

        /// <summary>
        /// Enable compact public review layout CSS.
        /// </summary>
        public bool PublicCompactReviewLayout { get; set; }
    }
}
