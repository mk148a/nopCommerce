using Nop.Services.Seo;
using Nop.Services.Catalog;
using Microsoft.AspNetCore.Http;
using System.Text.RegularExpressions;
using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using Nop.Plugin.Widgets.CustomProductReviews.Services;
using Nop.Services.Plugins;

namespace Nop.Plugin.Widgets.CustomProductReviews.Infrastructure
{
    public class PluginNopStartup : INopStartup
    {
        public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<RazorViewEngineOptions>(options =>
            {
               
                options.ViewLocationExpanders.Add(new ViewLocationExpander());

            });
         

            //register services and interfaces
            //services.AddScoped<CustomModelFactory, ICustomerModelFactory>();
            services.AddScoped<IProductReviewVideoService, ProductReviewVideoService>();
            services.AddScoped<ICustomProductReviewMappingService, CustomProductReviewMappingService>();
            services.AddSingleton<IBackgroundQueue,BackgroundQueue>();
            services.AddHostedService<QueueService>();
            services.AddScoped<IPluginService, PluginService>();
        }

        public void Configure(IApplicationBuilder application)
        {
            // HOOD 1.09: Handle old /{lang}/productreviews/{productId} URLs before nopCommerce logs them as 404.
            // Existing products are consolidated to the product page + #product-reviews.
            // Missing/deleted products return 410 Gone, which is cleaner for SEO than repeated 404 logs.
            application.Use(async (context, next) =>
            {
                var path = context.Request.Path.Value ?? string.Empty;
                var match = Regex.Match(path, @"^/(?:([a-z]{2}(?:-[a-z]{2})?)/)?productreviews/(\d+)/?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

                if (!match.Success)
                {
                    await next();
                    return;
                }

                if (!int.TryParse(match.Groups[2].Value, out var productId) || productId <= 0)
                {
                    context.Response.StatusCode = StatusCodes.Status410Gone;
                    return;
                }

                var productService = context.RequestServices.GetService<IProductService>();
                var urlRecordService = context.RequestServices.GetService<IUrlRecordService>();

                if (productService == null || urlRecordService == null)
                {
                    await next();
                    return;
                }

                var product = await productService.GetProductByIdAsync(productId);
                if (product == null || product.Deleted || !product.Published)
                {
                    context.Response.StatusCode = StatusCodes.Status410Gone;
                    return;
                }

                var seName = await urlRecordService.GetSeNameAsync(product);
                if (string.IsNullOrWhiteSpace(seName))
                {
                    context.Response.StatusCode = StatusCodes.Status410Gone;
                    return;
                }

                var langPrefix = match.Groups[1].Success ? match.Groups[1].Value.ToLowerInvariant() + "/" : string.Empty;
                var targetUrl = $"/{langPrefix}{seName}#product-reviews";
                context.Response.StatusCode = StatusCodes.Status301MovedPermanently;
                context.Response.Headers["Location"] = targetUrl;
            });
        }

        public int Order => 700;
    }
}