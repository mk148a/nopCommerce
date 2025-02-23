using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Configuration;
using Nop.Core.Infrastructure;
using Nop.Core.Infrastructure.DependencyManagement;
using Nop.Plugin.Misc.GoogleBotAggregator.Services;

namespace Nop.Plugin.Misc.GoogleBotAggregator.Infrastructure;

/// <summary>
/// Represents object for the configuring services on application startup
/// </summary>
public class DependencyRegistrar : IDependencyRegistrar
{
    /// <summary>
    /// Register services and interfaces
    /// </summary>
    /// <param name="services">Collection of service descriptors</param>
    /// <param name="typeFinder">Type finder</param>
    /// <param name="appSettings">App settings</param>
    public virtual void Register(IServiceCollection services, ITypeFinder typeFinder, AppSettings appSettings)
    {
        services.AddScoped<IGoogleBotService, GoogleBotService>();
        services.AddScoped<GoogleBotEventConsumer>();
    }

    /// <summary>
    /// Gets order of this dependency registrar implementation
    /// </summary>
    public int Order => 1;
} 