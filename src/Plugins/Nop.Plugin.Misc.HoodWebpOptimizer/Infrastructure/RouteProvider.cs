using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Misc.HoodWebpOptimizer.Infrastructure;

public sealed class RouteProvider : IRouteProvider
{
    public void RegisterRoutes(IEndpointRouteBuilder endpointRouteBuilder)
    {
        endpointRouteBuilder.MapControllerRoute(HoodWebpDefaults.ConfigurationRouteName,
            "Admin/HoodWebpOptimizer/Configure",
            new { controller = "HoodWebpOptimizer", action = "Configure", area = AreaNames.ADMIN });
        endpointRouteBuilder.MapControllerRoute("Plugin.Misc.HoodWebpOptimizer.DownloadOriginal",
            "Admin/HoodWebpOptimizer/Original/{pictureId:int}",
            new { controller = "HoodWebpOptimizer", action = "DownloadOriginal", area = AreaNames.ADMIN });
    }

    public int Priority => 0;
}
