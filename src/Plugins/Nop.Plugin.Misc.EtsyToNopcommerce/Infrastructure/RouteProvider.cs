using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Misc.EtsyToNopcommerce.Infrastructure
{
    /// <summary>
    /// Represents plugin route provider
    /// </summary>
    public class RouteProvider : IRouteProvider
    {
        /// <summary>
        /// Register routes
        /// </summary>
        /// <param name="endpointRouteBuilder">Route builder</param>
        public void RegisterRoutes(IEndpointRouteBuilder endpointRouteBuilder)
        {
            endpointRouteBuilder.MapControllerRoute("Plugin.Misc.EtsyToNopcommerce.Configure",
                "Admin/EtsyToNopcommerce/Configure",
                new { controller = "EtsyToNopcommerce", action = "Configure" });

            //endpointRouteBuilder.MapControllerRoute("etsy-yetkilendir",
            //    "Admin/EtsyToNopcommerce/etsy-yetkilendir",
            //    new { controller = "EtsyToNopcommerce", action = "Callback" });
            endpointRouteBuilder.MapControllerRoute("Plugin.Misc.EtsyToNopcommerce.InsertEtsyReviewsToNopcommerce",
                "Admin/EtsyToNopcommerce/InsertEtsyReviewsToNopcommerce",
                new { controller = "EtsyReviews", action = "InsertEtsyReviewsToNopcommerce" });



        }

        /// <summary>
        /// Gets a priority of route provider
        /// </summary>
        public int Priority => 0;
    }
}