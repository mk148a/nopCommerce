using Nop.Core;

namespace Nop.Plugin.Widgets.CustomProductReviews.Domains
{
    public partial class CustomProductReviewMapping : BaseEntity {

        public int ProductReviewId { get; set; }
        public int? PictureId { get; set; }
        /// <summary>References the plugin-owned review-video store.</summary>
        public int? ProductReviewVideoId { get; set; }
        // Retained only to read installations created by the former Video table.
        public int? VideoId { get; set; }
        public int DisplayOrder { get; set; }

    }
}
