using Nop.Core.Domain.Cms;
using Nop.Core.Caching;
using Nop.Services.Cms;
using Nop.Services.Configuration;
using Nop.Services.Plugins;
using Nop.Web.Framework.Infrastructure;
using Nop.Web.Framework.Models.Cms;

namespace Nop.Plugin.Widgets.HoodAdsSignalTracking;

/// <summary>
/// Supplies standard GA4 commerce events from widget zones. No customer-identifying data is emitted.
/// </summary>
public sealed class HoodAdsSignalTrackingPlugin : BasePlugin, IWidgetPlugin
{
    private readonly WidgetSettings _widgetSettings;
    private readonly ISettingService _settingService;
    private readonly IStaticCacheManager _staticCacheManager;

    public HoodAdsSignalTrackingPlugin(WidgetSettings widgetSettings, ISettingService settingService,
        IStaticCacheManager staticCacheManager)
    {
        _widgetSettings = widgetSettings;
        _settingService = settingService;
        _staticCacheManager = staticCacheManager;
    }

    public bool HideInWidgetList => false;

    public Task<IList<string>> GetWidgetZonesAsync()
        => Task.FromResult<IList<string>>([
            PublicWidgetZones.ProductDetailsBeforeCollateral,
            PublicWidgetZones.BodyEndHtmlTagBefore
        ]);

    public Type GetWidgetViewComponent(string widgetZone)
    {
        ArgumentNullException.ThrowIfNull(widgetZone);
        return typeof(Components.HoodAdsSignalTrackingViewComponent);
    }

    public override async Task InstallAsync()
    {
        if (!_widgetSettings.ActiveWidgetSystemNames.Contains(PluginDescriptor.SystemName))
        {
            _widgetSettings.ActiveWidgetSystemNames.Add(PluginDescriptor.SystemName);
            await _settingService.SaveSettingAsync(_widgetSettings);
        }

        await _staticCacheManager.RemoveByPrefixAsync(WidgetModelDefaults.WidgetPrefixCacheKey);

        await base.InstallAsync();
    }

    public override async Task UninstallAsync()
    {
        if (_widgetSettings.ActiveWidgetSystemNames.Contains(PluginDescriptor.SystemName))
        {
            _widgetSettings.ActiveWidgetSystemNames.Remove(PluginDescriptor.SystemName);
            await _settingService.SaveSettingAsync(_widgetSettings);
        }

        await _staticCacheManager.RemoveByPrefixAsync(WidgetModelDefaults.WidgetPrefixCacheKey);

        await base.UninstallAsync();
    }
}
