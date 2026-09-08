using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Misc.StripeBnplCore.Infrastructure;

public sealed class RouteProvider : IRouteProvider
{
    public void RegisterRoutes(IEndpointRouteBuilder endpointRouteBuilder)
    {
        endpointRouteBuilder.MapControllerRoute(
            StripeBnplDefaults.WebhookRouteName,
            StripeBnplDefaults.WebhookPath,
            new { controller = "StripeBnplWebhook", action = "Webhook" });
        endpointRouteBuilder.MapControllerRoute(
            StripeBnplDefaults.ReturnRouteName,
            StripeBnplDefaults.ReturnPath,
            new { controller = "StripeBnplReturn", action = "Return" });
        endpointRouteBuilder.MapControllerRoute(
            StripeBnplDefaults.CancelRouteName,
            StripeBnplDefaults.CancelPath,
            new { controller = "StripeBnplReturn", action = "Cancel" });
        endpointRouteBuilder.MapControllerRoute(
            StripeBnplDefaults.StatusRouteName,
            StripeBnplDefaults.StatusPath,
            new { controller = "StripeBnplReturn", action = "Status" });
    }

    public int Priority => 0;
}
