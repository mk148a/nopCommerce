using Nop.Services.Configuration;
using Nop.Services.Logging;
using Nop.Services.ScheduleTasks;

namespace Nop.Plugin.Misc.Brevo.Services;

/// <summary>
/// Checks the opt-in seasonal calendar. It prepares coupons only when the planner is enabled.
/// </summary>
public sealed class SeasonalCampaignTask : IScheduleTask
{
    private readonly ILogger _logger;
    private readonly SeasonalCampaignService _seasonalCampaignService;
    private readonly ISettingService _settingService;

    public SeasonalCampaignTask(ILogger logger, SeasonalCampaignService seasonalCampaignService,
        ISettingService settingService)
    {
        _logger = logger;
        _seasonalCampaignService = seasonalCampaignService;
        _settingService = settingService;
    }

    public async Task ExecuteAsync()
    {
        var settings = await _settingService.LoadSettingAsync<CampaignAutomationSettings>();
        if (!settings.Enabled)
            return;

        var result = await _seasonalCampaignService.PrepareNextCampaignAsync(settings, requireWithinLeadTime: true);
        if (!result.WasCreated || string.IsNullOrWhiteSpace(result.BrevoCampaignError))
            return;

        await _logger.WarningAsync($"Brevo seasonal campaign '{result.Occasion.Key}' created coupon #{result.Discount.Id}, " +
            $"but Brevo campaign preparation failed: {result.BrevoCampaignError}");
    }
}
