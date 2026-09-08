using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Misc.NopWebApi
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
            endpointRouteBuilder.MapControllerRoute("GetAllProducts",
                "Admin/NopWebApi/GetAllProducts",
                new { controller = "NopWebApi", action = "GetAllProducts" });


            endpointRouteBuilder.MapControllerRoute("GetAllOrders",
                "Admin/NopWebApi/GetAllOrders",
                new { controller = "NopWebApi", action = "GetAllOrders" });

        }

        /// <summary>
        /// Gets a priority of route provider
        /// </summary>
        public int Priority => 0;
    }
}