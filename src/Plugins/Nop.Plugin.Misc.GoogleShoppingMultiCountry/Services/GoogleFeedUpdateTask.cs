using Nop.Core;
using Nop.Core.Domain.Stores;
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
    private readonly IStoreContext _storeContext;
    private readonly IStoreService _storeService;

    public GoogleFeedUpdateTask(IGoogleService googleService,
        IPluginService pluginService,
        IStoreContext storeContext,
        IStoreService storeService)
    {
        _googleService = googleService;
        _pluginService = pluginService;
        _storeContext = storeContext;
        _storeService = storeService;
    }

    public async Task ExecuteAsync()
    {
        await _googleService.CreateTaxonomyEntityAsync();

        var descriptor = await _pluginService.GetPluginDescriptorBySystemNameAsync<IPlugin>("Nop.Plugin.Misc.GoogleShoppingMultiCountry");
        if (descriptor?.Instance<IPlugin>() is not GoogleShoppingMultiCountry plugin)
            throw new InvalidOperationException("The Google Shopping Multi Country plugin could not be loaded.");

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

        foreach (var store in stores)
            await plugin.GenerateStaticFileAsync(store);
    }
}
