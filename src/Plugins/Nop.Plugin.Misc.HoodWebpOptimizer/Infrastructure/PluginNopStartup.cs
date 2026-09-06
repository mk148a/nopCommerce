using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using Nop.Services.Media;
using Nop.Plugin.Misc.HoodWebpOptimizer.Services;

namespace Nop.Plugin.Misc.HoodWebpOptimizer.Infrastructure;

public sealed class PluginNopStartup : INopStartup
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IAdaptiveWebpEncoder, AdaptiveWebpEncoder>();
        services.AddSingleton<IOriginalPictureStore, OriginalPictureStore>();
        services.AddScoped<IExistingPictureOptimizer, ExistingPictureOptimizer>();
        // ScheduleTaskRunner resolves the configured task type directly. Registering every
        // task as IScheduleTask makes nopCommerce select only one plugin implementation
        // and causes the administration collision warning.

        // AzurePictureService changes persistence semantics. Do not replace it; the configuration UI reports this safe no-op.
        var azureConnection = configuration["AzureBlobConfig:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(azureConnection))
            return;

        services.AddScoped<AdaptiveWebpPictureService>();
        services.AddScoped<IPictureService>(provider => provider.GetRequiredService<AdaptiveWebpPictureService>());
    }

    public void Configure(IApplicationBuilder application)
    {
    }

    // Nop.Web.Framework registers PictureService at order 2000. Register one step
    // later so this plugin's safe local decorator is not silently overwritten.
    public int Order => 2001;
}
