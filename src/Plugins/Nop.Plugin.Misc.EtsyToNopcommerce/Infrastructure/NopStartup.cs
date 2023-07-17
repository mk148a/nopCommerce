using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using Nop.Plugin.Misc.EtsyToNopcommerce.Services;
using Nop.Services.Plugins;

namespace Nop.Plugin.Misc.EtsyToNopcommerce.Infrastructure
{
    public class NopStartup : INopStartup
    {
        public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<RazorViewEngineOptions>(options =>
            {
                options.ViewLocationExpanders.Add(new ViewLocationExpander());
            });

            //register services and interfaces
            services.AddScoped<IProductReviewsEtsyReviewService, ProductReviewsEtsyReviewService>();
            services.AddScoped<ICustomProductReviewMappingService, CustomProductReviewMappingService>();
            services.AddScoped<IProductReviewsTransactionsMappingService, ProductReviewsTransactionsMappingService>();
            
            services.AddSingleton<IBackgroundQueue, BackgroundQueue>();
            services.AddHostedService<QueueService>();
          

        }

        public void Configure(IApplicationBuilder application)
        {
        }

        public int Order => 11;
    }
}