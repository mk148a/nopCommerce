using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using Nop.Plugin.Misc.ProductCatalogCreator.Services;

namespace Nop.Plugin.Misc.ProductCatalogCreator.Infrastructure
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
            services.AddScoped<ProductCatalogService, ProductCatalogService>();
        }

        public void Configure(IApplicationBuilder application)
        {
        }

        public int Order => 11;
    }
}