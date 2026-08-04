using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Misc.GoogleBotAggregator.Infrastructure;

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
        //endpointRouteBuilder.MapControllerRoute(name: GoogleBotDefaults.ConfigurationRouteName,
        //    pattern: "Admin/GoogleBotAggregator/Configure",
        //    defaults: new { controller = "GoogleBotAggregator", action = "Configure", area = "Admin" });
    }

    /// <summary>
    /// Gets a priority of route provider
    /// </summary>
    public int Priority => 0;
}