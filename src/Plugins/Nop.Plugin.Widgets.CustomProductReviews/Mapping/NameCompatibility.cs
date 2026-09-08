using System;
using System.Collections.Generic;
using Nop.Data.Mapping;
using Nop.Plugin.Widgets.CustomProductReviews.Domains;

namespace Nop.Plugin.Widgets.CustomProductReviews.Mapping
{
    public partial class NameCompatibility : INameCompatibility
    {
        // Keep review-upload videos isolated from nopCommerce's catalogue Video table.
        // The production schema already uses these names; the compatibility map makes
        // the existing review-media service resolve the same persistent store.
        public Dictionary<Type, string> TableNames => new Dictionary<Type, string>
        {
            [typeof(Video)] = "ProductReviewVideo",
            [typeof(VideoBinary)] = "ProductReviewVideoBinary"
        };

        public Dictionary<(Type, string), string> ColumnName => new Dictionary<(Type, string), string>();
    }
}
