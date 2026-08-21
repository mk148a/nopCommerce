using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Widgets.CustomProductReviews.Infrastructure;

/// <summary>
/// Handles legacy URLs already requested by search crawlers and redirects
/// them to the canonical localized product page.
/// </summary>
public sealed class ReviewSeoRouteProvider : IRouteProvider
{
    public void RegisterRoutes(IEndpointRouteBuilder endpointRouteBuilder)
    {
        endpointRouteBuilder.MapControllerRoute(
            name: "Plugin.Widgets.CustomProductReviews.LegacyReviewSeoRedirect",
            pattern: "{language}/productreviews/{reviewId:int}",
            defaults: new { controller = "ReviewSeoRedirect", action = "ProductReview" });
    }

    // Register before the generic URL route so an already-crawled legacy URL
    // is not converted to the site's generic page-not-found redirect.
    public int Priority => 100;
}
