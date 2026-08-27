using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Infrastructure;

/// <summary>
/// Runs after canonical-host normalization and before endpoint execution, so
/// the legacy AU advertising URL has one permanent local target without
/// bypassing the established host-canonical policy.
/// </summary>
public sealed class LegacyAuWoodenArrowsRedirectNopStartup : INopStartup
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    public void Configure(IApplicationBuilder application)
    {
        application.Use(async (context, next) =>
        {
            if (LegacyAuWoodenArrowsRedirect.TryBuildLocation(context.Request, out var location))
            {
                context.Response.StatusCode = StatusCodes.Status301MovedPermanently;
                context.Response.Headers.Location = location;
                return;
            }

            await next();
        });
    }

    public int Order => 452;
}
