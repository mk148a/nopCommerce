using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;
using Nop.Web.Infrastructure;

namespace NopStation.Plugin.Widgets.GoogleTagManager;

public class RouteProvider : BaseRouteProvider, IRouteProvider
{
	public int Priority => 1;

	public void RegisterRoutes(IEndpointRouteBuilder endpointRouteBuilder)
	{
		string languageRoutePattern = GetLanguageRoutePattern();
		MapGtmRoute(endpointRouteBuilder, languageRoutePattern, "ProductDetails", "ProductDetails");
		MapGtmRoute(endpointRouteBuilder, languageRoutePattern, "ProductDetailsViewItem", "ProductDetailsViewItem");
		MapGtmRoute(endpointRouteBuilder, languageRoutePattern, "ShoppingCartDetails", "ShoppingCartDetails");
		MapGtmRoute(endpointRouteBuilder, languageRoutePattern, "GetProducts", "GetProducts");
	}

	private static void MapGtmRoute(IEndpointRouteBuilder endpointRouteBuilder, string languageRoutePattern,
		string routeSuffix, string action)
	{
		endpointRouteBuilder.MapControllerRoute($"NopStation.GoogleTagManager.GtmEventSend.{routeSuffix}",
			languageRoutePattern + "/GtmEventSend/" + routeSuffix, new
			{
				controller = "GtmEventSend",
				action
			});
	}
}
