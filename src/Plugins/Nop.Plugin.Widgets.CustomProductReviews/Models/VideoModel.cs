using Nop.Web.Framework.Models;

namespace Nop.Plugin.Widgets.CustomProductReviews.Models
{
    public partial record VideoModel : BaseNopModel
    {
        public string ImageUrl { get; set; }

        public string ThumbImageUrl { get; set; }

        public string FullSizeImageUrl { get; set; }

        public string Title { get; set; }

        public string AlternateText { get; set; }

        public string MimeType { get; set; }
    }
}
