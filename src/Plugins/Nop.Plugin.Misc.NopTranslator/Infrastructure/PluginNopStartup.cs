using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using Nop.Plugin.Misc.NopTranslator.Services;

namespace Nop.Plugin.Misc.NopTranslator.Infrastructure
{
    public class PluginNopStartup : INopStartup
    {
        public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
           
            //register services and interfaces
            services.AddScoped<ITranslateService,TranslateService>();
            services.AddSingleton<ITranslationProgressService, TranslationProgressService>();
        }

        public void Configure(IApplicationBuilder application)
        {
        }

        public int Order => 11;
    }
}