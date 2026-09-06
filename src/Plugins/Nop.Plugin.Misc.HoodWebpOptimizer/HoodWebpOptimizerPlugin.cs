using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Nop.Core.Domain.ScheduleTasks;
using Nop.Services.Configuration;
using Nop.Services.Plugins;
using Nop.Services.ScheduleTasks;

namespace Nop.Plugin.Misc.HoodWebpOptimizer;

public sealed class HoodWebpOptimizerPlugin : BasePlugin
{
    private readonly IActionContextAccessor _actionContextAccessor;
    private readonly IUrlHelperFactory _urlHelperFactory;
    private readonly ISettingService _settingService;
    private readonly IScheduleTaskService _scheduleTaskService;

    public HoodWebpOptimizerPlugin(IActionContextAccessor actionContextAccessor,
        IUrlHelperFactory urlHelperFactory,
        ISettingService settingService,
        IScheduleTaskService scheduleTaskService)
    {
        _actionContextAccessor = actionContextAccessor;
        _urlHelperFactory = urlHelperFactory;
        _settingService = settingService;
        _scheduleTaskService = scheduleTaskService;
    }

    public override string GetConfigurationPageUrl() => _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext)
        .RouteUrl(HoodWebpDefaults.ConfigurationRouteName);

    public override async Task InstallAsync()
    {
        await _settingService.SaveSettingAsync(new HoodWebpSettings());
        if (await _scheduleTaskService.GetTaskByTypeAsync(HoodWebpDefaults.BatchTaskType) is null)
            await _scheduleTaskService.InsertTaskAsync(new ScheduleTask
            {
                Name = "Hood adaptive WebP legacy image batch",
                Type = HoodWebpDefaults.BatchTaskType,
                Seconds = 900,
                // Catalog backfill is explicitly opt-in. New uploads are still optimized
                // according to HoodWebpSettings.Enabled; this task exists only for legacy media.
                Enabled = false,
                StopOnError = false,
                LastEnabledUtc = DateTime.UtcNow
            });
        await base.InstallAsync();
    }

    public override async Task UninstallAsync()
    {
        var task = await _scheduleTaskService.GetTaskByTypeAsync(HoodWebpDefaults.BatchTaskType);
        if (task is not null)
            await _scheduleTaskService.DeleteTaskAsync(task);
        await _settingService.DeleteSettingAsync<HoodWebpSettings>();
        await base.UninstallAsync();
    }
}
