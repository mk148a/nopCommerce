using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using NopStation.Plugin.Misc.Core.Infrastructure;
using NopStation.Plugin.Widgets.GoogleTagManager.Services;

namespace NopStation.Plugin.Widgets.GoogleTagManager.Infrastructure;

public class PluginNopStartup : INopStartup
{
	public int Order => 11;

	public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
	{
		CoreStartupServices.AddNopStationServices(services, "NopStation.Plugin.Widgets.GoogleTagManager", false, false);
		services.AddScoped<IGTMService, GTMService>();
	}

	public void Configure(IApplicationBuilder application)
	{
	}
}
