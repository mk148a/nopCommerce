using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using Nop.Plugin.Misc.HoodLocalizationSeo.Factories;
using Nop.Plugin.Misc.HoodLocalizationSeo.Services;
using Nop.Web.Factories;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Infrastructure;

public sealed class HoodNopStartup : INopStartup
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IBlogLocalizationService, BlogLocalizationService>();
        services.AddScoped<ILocalizationResourceInstaller, LocalizationResourceInstaller>();
        services.AddScoped<LocalizedBlogRedirectFilter>();

        Decorate<IBlogModelFactory, LocalizedBlogModelFactory>(services);
        Decorate<ISitemapModelFactory, LocalizedSitemapModelFactory>(services);

        services.Configure<RazorViewEngineOptions>(options =>
            options.ViewLocationExpanders.Insert(0, new HoodViewLocationExpander()));
        services.Configure<MvcOptions>(options =>
            options.Filters.AddService<LocalizedBlogRedirectFilter>());
    }

    public void Configure(IApplicationBuilder application)
    {
    }

    public int Order => 3000;

    private static void Decorate<TService, TDecorator>(IServiceCollection services)
        where TService : class
        where TDecorator : class, TService
    {
        var descriptor = services.LastOrDefault(item => item.ServiceType == typeof(TService))
            ?? throw new InvalidOperationException($"{typeof(TService).FullName} is not registered.");
        var implementationType = descriptor.ImplementationType
            ?? throw new InvalidOperationException(
                $"Cannot decorate factory/instance registration for {typeof(TService).FullName}.");

        services.Remove(descriptor);
        services.AddScoped(implementationType);
        services.AddScoped<TService>(provider =>
            ActivatorUtilities.CreateInstance<TDecorator>(provider,
                (TService)provider.GetRequiredService(implementationType)));
    }
}
