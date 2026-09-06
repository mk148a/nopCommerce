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
		endpointRouteBuilder.MapControllerRoute("NopStation.GoogleTagManager.GtmEventSend", languageRoutePattern + "/GtmEventSend/ProductDetails", new
		{
			controller = "GtmEventSend",
			action = "ProductDetails"
		});
	}
}
