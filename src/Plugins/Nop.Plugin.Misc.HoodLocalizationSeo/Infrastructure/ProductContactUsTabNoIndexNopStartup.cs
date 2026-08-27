using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Infrastructure;

/// <summary>
/// Emits an HTTP crawler directive for narrowly scoped legacy public GET/HEAD endpoints.
/// It runs after canonical-host handling and before endpoint execution.
/// </summary>
public sealed class ProductContactUsTabNoIndexNopStartup : INopStartup
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    public void Configure(IApplicationBuilder application)
    {
        application.Use(async (context, next) =>
        {
            ProductContactUsTabNoIndex.TryApply(context);
            await next();
        });
    }

    public int Order => 451;
}
