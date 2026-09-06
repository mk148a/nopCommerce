using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using Nop.Plugin.Misc.StripeBnplCore.Services;
using Nop.Services.ScheduleTasks;

namespace Nop.Plugin.Misc.StripeBnplCore.Infrastructure;

public sealed class PluginNopStartup : INopStartup
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IBnplCatalogRiskService, BnplCatalogRiskService>();
        services.AddScoped<IBnplCartContextService, BnplCartContextService>();
        services.AddScoped<IBnplEligibilityService, BnplEligibilityService>();
        services.AddScoped<IBnplProductEligibilityStore, BnplProductEligibilityStore>();
        services.AddScoped<IStripeBnplCheckoutService, StripeBnplCheckoutService>();
        services.AddScoped<IStripeBnplCheckoutSessionStore, StripeBnplCheckoutSessionStore>();
        services.AddScoped<IStripeBnplConfigurationClient, StripeBnplConfigurationClient>();
        services.AddScoped<IStripeBnplConversionService, StripeBnplConversionService>();
        services.AddScoped<IStripeBnplEnvironmentGuard, StripeBnplEnvironmentGuard>();
        services.AddScoped<IStripeBnplPaymentClient, StripeBnplPaymentClient>();
        services.AddScoped<IStripeBnplPaymentFinalizer, StripeBnplPaymentFinalizer>();
        services.AddScoped<IStripeBnplPaymentRecordStore, StripeBnplPaymentRecordStore>();
        services.AddScoped<IStripeBnplFeeReconciliationService, StripeBnplFeeReconciliationService>();
        services.AddScoped<IStripeBnplPendingOrderCleanupService, StripeBnplPendingOrderCleanupService>();
        services.AddScoped<StripeBnplFeeReconciliationTask>();
        services.AddScoped<IScheduleTask>(provider => provider.GetRequiredService<StripeBnplFeeReconciliationTask>());
        services.AddScoped<StripeBnplPendingOrderCleanupTask>();
        services.AddScoped<IScheduleTask>(provider => provider.GetRequiredService<StripeBnplPendingOrderCleanupTask>());
        services.AddScoped<IStripeBnplService, StripeBnplService>();
        services.AddScoped<IStripeBnplSessionClient, StripeBnplSessionClient>();
        services.AddScoped<IStripeBnplWebhookEventStore, StripeBnplWebhookEventStore>();
        services.AddScoped<IStripeBnplWebhookService, StripeBnplWebhookService>();
    }

    public void Configure(IApplicationBuilder application)
    {
    }

    public int Order => 11;
}
