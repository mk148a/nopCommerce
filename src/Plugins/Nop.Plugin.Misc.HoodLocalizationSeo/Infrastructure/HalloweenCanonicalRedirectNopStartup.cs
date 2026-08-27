using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using Nop.Services.Stores;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Infrastructure;

/// <summary>
/// Places the route-specific www canonical redirect before endpoint execution
/// without changing the registration order of the main plugin startup.
/// </summary>
public sealed class HalloweenCanonicalRedirectNopStartup : INopStartup
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    public void Configure(IApplicationBuilder application)
    {
        // This must run before endpoint execution and before nopCommerce's
        // store-host fallback. The latter turns an unregistered www alias
        // into /page-not-found before the route controller can run.
        application.Use(async (context, next) =>
        {
            if (!HalloweenLandingCanonicalRedirect.IsLandingGetOrHead(context.Request.Path,
                    context.Request.Method, out var language))
            {
                await next();
                return;
            }

            var storeService = context.RequestServices.GetRequiredService<IStoreService>();
            var stores = await storeService.GetAllStoresAsync();
            if (!HalloweenLandingCanonicalRedirect.TryGetCanonicalStoreForHost(storeService, stores,
                    context.Request.Host, out var canonicalOrigin, out var hostKind))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            if (hostKind == CanonicalHostKind.Canonical)
            {
                await next();
                return;
            }

            if (hostKind == CanonicalHostKind.WwwAlias)
            {
                context.Response.StatusCode = StatusCodes.Status301MovedPermanently;
                context.Response.Headers.Location = HalloweenLandingCanonicalRedirect.AppendAllowedTrackingQuery(
                    HalloweenLandingCanonicalRedirect.BuildCanonicalUrl(canonicalOrigin, language),
                    context.Request.Query);
                return;
            }

            // A registered but non-www alias belongs to this store, but this
            // narrow repair must not choose a canonicalization rule for it.
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
        });
    }

    public int Order => 450;
}
