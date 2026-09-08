using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Payments.Stripe.Infrastructure;

/// <summary>
/// Registers the anonymous Stripe webhook endpoint separately from the admin controller.
/// </summary>
public class RouteProvider : IRouteProvider
{
    public void RegisterRoutes(IEndpointRouteBuilder endpointRouteBuilder)
    {
        endpointRouteBuilder.MapControllerRoute(
            name: "Payments.Stripe.Webhook",
            pattern: StripePaymentDefaults.WebhookPath,
            defaults: new { controller = "StripeWebhook", action = "WebhookHandler" });
    }

    public int Priority => 0;
}
