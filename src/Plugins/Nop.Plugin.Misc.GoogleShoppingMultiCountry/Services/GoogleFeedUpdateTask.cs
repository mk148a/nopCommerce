using Nop.Core;
using Nop.Core.Domain.Stores;
using Nop.Services.Configuration;
using Nop.Services.Plugins;
using Nop.Services.ScheduleTasks;
using Nop.Services.Stores;

namespace Nop.Plugin.Misc.GoogleShoppingMultiCountry.Services;

/// <summary>
/// Regenerates the static Google Shopping feeds for the active store scope.
/// </summary>
public class GoogleFeedUpdateTask : IScheduleTask
{
    private readonly IGoogleService _googleService;
    private readonly IPluginService _pluginService;
    private readonly ISettingService _settingService;
    private readonly IStoreContext _storeContext;
    private readonly IStoreService _storeService;

    public GoogleFeedUpdateTask(IGoogleService googleService,
        IPluginService pluginService,
        ISettingService settingService,
        IStoreContext storeContext,
        IStoreService storeService)
    {
        _googleService = googleService;
        _pluginService = pluginService;
        _settingService = settingService;
        _storeContext = storeContext;
        _storeService = storeService;
    }

    public async Task ExecuteAsync()
    {
        var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
        var stores = new List<Store>();

        if (storeScope > 0)
        {
            var store = await _storeService.GetStoreByIdAsync(storeScope);
            if (store is not null)
                stores.Add(store);
        }
        else
        {
            stores.AddRange(await _storeService.GetAllStoresAsync());
        }

        var configuredStores = new List<Store>();
        foreach (var store in stores)
        {
            var settings = await _settingService.LoadSettingAsync<GoogleShoppingMultiCountrySettings>(store.Id);
            if (!string.IsNullOrWhiteSpace(settings.DefaultGoogleCategoryId))
                configuredStores.Add(store);
        }

        // An optional feed task must not fail the global scheduler before the
        // merchant has selected a real Google product category in its settings.
        if (configuredStores.Count == 0)
            return;

        await _googleService.CreateTaxonomyEntityAsync();

        // Keep the lookup aligned with the installed plugin identity in plugin.json.
        // Changing a deployed SystemName creates a second, stale plugin record.
        var descriptor = await _pluginService.GetPluginDescriptorBySystemNameAsync<IPlugin>("Nop.Plugin.Misc.GoogleShoppingMultiCountry");
        if (descriptor?.Instance<IPlugin>() is not GoogleShoppingMultiCountry plugin)
            throw new InvalidOperationException("The Google Shopping Multi Country plugin could not be loaded.");

        foreach (var store in configuredStores)
            await plugin.GenerateStaticFileAsync(store);
    }
}
