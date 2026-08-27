using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using Nop.Services.Stores;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Infrastructure;

/// <summary>
/// Runs before nopCommerce host fallback and endpoint execution so the
/// registered www storefront alias has one canonical origin for HTML,
/// sitemap and video-watch discovery.
/// </summary>
public sealed class PublicStorefrontCanonicalRedirectNopStartup : INopStartup
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    public void Configure(IApplicationBuilder application)
    {
        application.Use(async (context, next) =>
        {
            if (!PublicStorefrontCanonicalRedirect.IsEligibleRequest(context.Request) ||
                !PublicStorefrontCanonicalRedirect.IsPotentialWwwAlias(context.Request.Host))
            {
                await next();
                return;
            }

            var storeService = context.RequestServices.GetRequiredService<IStoreService>();
            var stores = await storeService.GetAllStoresAsync();
            if (!PublicStorefrontCanonicalRedirect.TryGetCanonicalStoreForWwwAlias(storeService, stores,
                    context.Request.Host, out var canonicalBase, out var hostKind))
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
                context.Response.Headers.Location = PublicStorefrontCanonicalRedirect.BuildCanonicalUrl(
                    canonicalBase, context.Request.Path, context.Request.QueryString);
                return;
            }

            // A registered host that is not the configured origin's explicit
            // www alias is ambiguous for this policy and must not be guessed.
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
        });
    }

    public int Order => 450;
}
