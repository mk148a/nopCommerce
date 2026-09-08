using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;

namespace Nop.Plugin.Misc.HoodAuthShield.Infrastructure;

/// <summary>Places the auth-only limiter after routing and before authentication/endpoints.</summary>
public sealed class PluginNopStartup : INopStartup
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<AuthBurstLimiter>();
    }

    public void Configure(IApplicationBuilder application)
    {
        application.UseMiddleware<AuthShieldMiddleware>();
    }

    public int Order => 450;
}
