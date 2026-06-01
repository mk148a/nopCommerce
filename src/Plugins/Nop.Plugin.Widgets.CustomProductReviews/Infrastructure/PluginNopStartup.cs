using Nop.Services.Seo;
using Nop.Services.Catalog;
using Microsoft.AspNetCore.Http;
using System.Text.RegularExpressions;
using System;
using Nop.Plugin.Widgets.CustomProductReviews;
using Nop.Services.Configuration;
using System.Text;
using System.IO;
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
            // HOOD 1.11: optionally inject review media thumbnails into the built-in admin ProductReview/List page
            // without overriding nopCommerce admin views. Controlled from plugin Configure page.
            application.Use(async (context, next) =>
            {
                var path = context.Request.Path.Value ?? string.Empty;
                var isAdminReviewList = Regex.IsMatch(path, @"/Admin/ProductReview/List/?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

                if (!isAdminReviewList || string.Equals(context.Request.Method, "POST", StringComparison.OrdinalIgnoreCase))
                {
                    await next();
                    return;
                }

                var settingService = context.RequestServices.GetService<ISettingService>();
                var settings = settingService == null ? null : await settingService.LoadSettingAsync<CustomProductReviewsSettings>();

                if (settings == null || !settings.AdminShowMediaOnProductReviewList)
                {
                    await next();
                    return;
                }

                var originalBody = context.Response.Body;
                await using var buffer = new MemoryStream();
                context.Response.Body = buffer;

                await next();

                buffer.Position = 0;
                var contentType = context.Response.ContentType ?? string.Empty;

                if (!contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase))
                {
                    buffer.Position = 0;
                    await buffer.CopyToAsync(originalBody);
                    context.Response.Body = originalBody;
                    return;
                }

                using var reader = new StreamReader(buffer, Encoding.UTF8);
                var html = await reader.ReadToEndAsync();
                var injection = BuildAdminReviewMediaInjection(settings);

                if (html.Contains("</body>", StringComparison.OrdinalIgnoreCase))
                    html = Regex.Replace(html, "</body>", injection + "</body>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                else
                    html += injection;

                var bytes = Encoding.UTF8.GetBytes(html);
                context.Response.Body = originalBody;
                context.Response.ContentLength = bytes.Length;
                await context.Response.Body.WriteAsync(bytes, 0, bytes.Length);
            });
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


        private static string BuildAdminReviewMediaInjection(CustomProductReviewsSettings settings)
        {
            var thumbSize = settings.AdminMediaThumbSize <= 0 ? 72 : settings.AdminMediaThumbSize;
            var maxItems = settings.AdminMediaMaxItemsPerReview <= 0 ? 6 : settings.AdminMediaMaxItemsPerReview;

            return $@"
<link rel=""stylesheet"" href=""/Plugins/Widgets.CustomProductReviews/Content/admin-review-media.css?v=111"" />
<script>document.documentElement.style.setProperty('--hood-admin-review-thumb-size', '{thumbSize}px'); window.hoodCustomReviewMediaMaxItems = {maxItems};</script>
<script src=""/Plugins/Widgets.CustomProductReviews/Content/js/admin-review-media.js?v=111""></script>";
        }

        public int Order => 700;
    }
}