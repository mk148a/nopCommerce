using Nop.Web.Framework.Mvc.ModelBinding;
using Nop.Web.Framework.Models;

namespace Nop.Plugin.Widgets.CustomProductReviews.Areas.Admin.Models
{
    public partial record ConfigurationModel : BaseNopModel
    {
        [NopResourceDisplayName("Plugins.Widgets.CustomProductReviews.Fields.WidgetZone")]
        public string WidgetZone { get; set; }

        [NopResourceDisplayName("Plugins.Widgets.CustomProductReviews.Fields.MaximumFile")]
        public int MaximumFile { get; set; }

        [NopResourceDisplayName("Plugins.Widgets.CustomProductReviews.Fields.MaximumSize")]
        public int MaximumSize { get; set; }

        [NopResourceDisplayName("Plugins.Widgets.CustomProductReviews.Fields.AdminShowMediaOnProductReviewList")]
        public bool AdminShowMediaOnProductReviewList { get; set; }

        [NopResourceDisplayName("Plugins.Widgets.CustomProductReviews.Fields.AdminMediaThumbSize")]
        public int AdminMediaThumbSize { get; set; }

        [NopResourceDisplayName("Plugins.Widgets.CustomProductReviews.Fields.AdminMediaMaxItemsPerReview")]
        public int AdminMediaMaxItemsPerReview { get; set; }

        [NopResourceDisplayName("Plugins.Widgets.CustomProductReviews.Fields.PublicCompactReviewLayout")]
        public bool PublicCompactReviewLayout { get; set; }
    }
}
