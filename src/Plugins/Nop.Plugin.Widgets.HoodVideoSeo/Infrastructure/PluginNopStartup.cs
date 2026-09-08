using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using Nop.Plugin.Widgets.HoodVideoSeo.Services;

namespace Nop.Plugin.Widgets.HoodVideoSeo.Infrastructure;

public sealed class PluginNopStartup : INopStartup
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RazorViewEngineOptions>(options =>
            options.ViewLocationExpanders.Add(new VideoSeoViewLocationExpander()));
        services.AddScoped<YouTubeEmbedResultFilter>();
        services.AddScoped<VideoSitemapDiscoveryResultFilter>();
        services.AddHttpClient(YouTubeVideoAvailabilityService.HttpClientName, client =>
        {
            client.BaseAddress = new Uri("https://www.youtube.com/");
            client.Timeout = YouTubeVideoAvailabilityService.HttpTimeout;
            client.DefaultRequestHeaders.UserAgent.ParseAdd("HoodArcheryShopVideoSeo/1.0");
        });
        services.AddSingleton<IYouTubeVideoAvailabilityService, YouTubeVideoAvailabilityService>();
        services.AddSingleton<IYouTubePublicationDateService, YouTubePublicationDateService>();
        services.Configure<MvcOptions>(options => options.Filters.AddService<YouTubeEmbedResultFilter>());
        services.Configure<MvcOptions>(options => options.Filters.AddService<VideoSitemapDiscoveryResultFilter>());
    }

    public void Configure(Microsoft.AspNetCore.Builder.IApplicationBuilder application)
    {
    }

    public int Order => 100;
}
