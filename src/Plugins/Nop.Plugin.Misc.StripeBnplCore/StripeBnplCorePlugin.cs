using Nop.Core;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Plugins;
using Nop.Services.ScheduleTasks;

namespace Nop.Plugin.Misc.StripeBnplCore;

public sealed class StripeBnplCorePlugin : BasePlugin, IMiscPlugin
{
    private readonly ILocalizationService _localizationService;
    private readonly ISettingService _settingService;
    private readonly IScheduleTaskService _scheduleTaskService;
    private readonly IWebHelper _webHelper;

    public StripeBnplCorePlugin(ILocalizationService localizationService,
        ISettingService settingService,
        IScheduleTaskService scheduleTaskService,
        IWebHelper webHelper)
    {
        _localizationService = localizationService;
        _settingService = settingService;
        _scheduleTaskService = scheduleTaskService;
        _webHelper = webHelper;
    }

    public override string GetConfigurationPageUrl() =>
        $"{_webHelper.GetStoreLocation()}Admin/StripeBnpl/Configure";

    public override async Task InstallAsync()
    {
        // Installation is deliberately non-operational: no secret, server allowlist,
        // live configuration or approval reference is supplied automatically.
        await _settingService.SaveSettingAsync(new StripeBnplSettings
        {
            UseSandbox = true,
            SandboxDatabaseName = "HoodArcheryShopStripeSandboxDb",
            StripeAccountCountryIso2 = "US",
            AfterpayMaximumFulfillmentDays = 0,
            PendingOrderCancellationHours = 24,
            WebhookSignatureToleranceSeconds = 300
        });

        await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
        {
            ["Plugins.Misc.StripeBnplCore.Name"] = "Stripe BNPL Core",
            ["Plugins.Misc.StripeBnplCore.Configuration"] = "Stripe BNPL configuration",
            ["Plugins.Misc.StripeBnplCore.Eligibility"] = "BNPL product eligibility"
        });

        if (await _scheduleTaskService.GetTaskByTypeAsync(StripeBnplDefaults.FeeReconciliationTaskType) == null)
        {
            await _scheduleTaskService.InsertTaskAsync(new()
            {
                Name = "Stripe BNPL fee reconciliation",
                Type = StripeBnplDefaults.FeeReconciliationTaskType,
                Seconds = 900,
                Enabled = true,
                StopOnError = false,
                LastEnabledUtc = DateTime.UtcNow
            });
        }
        await EnsurePendingOrderCleanupTaskAsync();

        await base.InstallAsync();
    }

    public override async Task UninstallAsync()
    {
        var task = await _scheduleTaskService.GetTaskByTypeAsync(StripeBnplDefaults.FeeReconciliationTaskType);
        if (task != null)
            await _scheduleTaskService.DeleteTaskAsync(task);
        var cleanupTask = await _scheduleTaskService.GetTaskByTypeAsync(StripeBnplDefaults.PendingOrderCleanupTaskType);
        if (cleanupTask != null)
            await _scheduleTaskService.DeleteTaskAsync(cleanupTask);
        await _settingService.DeleteSettingAsync<StripeBnplSettings>();
        await _localizationService.DeleteLocaleResourcesAsync("Plugins.Misc.StripeBnplCore");
        await base.UninstallAsync();
    }

    public override async Task UpdateAsync(string currentVersion, string targetVersion)
    {
        await EnsurePendingOrderCleanupTaskAsync();
        await base.UpdateAsync(currentVersion, targetVersion);
    }

    private async Task EnsurePendingOrderCleanupTaskAsync()
    {
        var settings = await _settingService.LoadSettingAsync<StripeBnplSettings>();
        var hasCancellationSetting = await _settingService.SettingExistsAsync(
            settings, item => item.PendingOrderCancellationHours);
        if (!hasCancellationSetting || settings.PendingOrderCancellationHours is < 1 or > 168)
        {
            settings.PendingOrderCancellationHours = 24;
            await _settingService.SaveSettingAsync(settings);
        }

        if (await _scheduleTaskService.GetTaskByTypeAsync(StripeBnplDefaults.PendingOrderCleanupTaskType) != null)
            return;

        await _scheduleTaskService.InsertTaskAsync(new()
        {
            Name = "Stripe BNPL pending order cleanup",
            Type = StripeBnplDefaults.PendingOrderCleanupTaskType,
            Seconds = 300,
            Enabled = true,
            StopOnError = false,
            LastEnabledUtc = DateTime.UtcNow
        });
    }
}
