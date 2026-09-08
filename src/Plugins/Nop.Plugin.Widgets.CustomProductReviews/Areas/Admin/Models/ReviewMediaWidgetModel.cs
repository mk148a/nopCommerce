using System.Collections.Generic;

namespace Nop.Plugin.Widgets.CustomProductReviews.Areas.Admin.Models;

public sealed class ReviewMediaWidgetModel
{
    public int ReviewId { get; init; }
    public IList<ReviewMediaItemModel> Items { get; init; } = new List<ReviewMediaItemModel>();
}
