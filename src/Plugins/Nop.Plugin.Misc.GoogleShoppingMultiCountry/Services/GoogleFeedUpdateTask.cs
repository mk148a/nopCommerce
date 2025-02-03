using System;
using System.Threading.Tasks;
using Nop.Core.Domain.Stores;
using Nop.Core;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Messages;
using Nop.Services.Plugins;
using Nop.Services.ScheduleTasks;
using Nop.Services.Stores;

namespace Nop.Plugin.Misc.GoogleShoppingMultiCountry.Services
{
    public class GoogleFeedUpdateTask : IScheduleTask
    {
        private readonly IGoogleService _googleService;
        private readonly IStoreContext _storeContext;
        private readonly IPluginService _pluginService;
        private readonly ILocalizationService _localizationService;
        private readonly IStoreService _storeService;
        private readonly INotificationService _notificationService;
        private readonly ILogger _logger;

       public GoogleFeedUpdateTask(IGoogleService googleService,IStoreContext storeContext,
           IPluginService pluginService,ILocalizationService localizationService,IStoreService storeService,
           INotificationService notificationService,ILogger logger)
        {
            _googleService = googleService;
            _storeContext = storeContext;
            _pluginService = pluginService;
            _localizationService = localizationService;
            _storeService = storeService;
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task ExecuteAsync()
        {
            try
            {
                // Taxonomy listesini güncelle
                await _googleService.CreateTaxonomyEntityAsync();

                //load settings for a chosen store scope
                var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();

                try
                {
                    //plugin
                    var pluginDescriptor = await _pluginService.GetPluginDescriptorBySystemNameAsync<IPlugin>("Misc.GoogleShoppingMultiCountry");
                    if (pluginDescriptor == null || pluginDescriptor.Instance<IPlugin>() is not GoogleShoppingMultiCountry plugin)
                        throw new Exception(await _localizationService.GetResourceAsync("Plugins.Feed.GoogleShoppingMultiCountry.ExceptionLoadPlugin"));

                    var stores = new List<Store>();
                    var storeById = await _storeService.GetStoreByIdAsync(storeScope);
                    if (storeScope > 0)
                        stores.Add(storeById);
                    else
                        stores.AddRange(await _storeService.GetAllStoresAsync());

                    foreach (var store in stores)
                        await plugin.GenerateStaticFileAsync(store);

                    _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Plugins.Feed.GoogleShoppingMultiCountry.SuccessResult"));
                }
                catch (Exception exc)
                {
                    _notificationService.ErrorNotification(exc.Message);
                    await _logger.ErrorAsync(exc.Message, exc);
                }
            }
            catch (Exception ex)
            {
                // Log the error
                throw;
            }
        }
    }
} 