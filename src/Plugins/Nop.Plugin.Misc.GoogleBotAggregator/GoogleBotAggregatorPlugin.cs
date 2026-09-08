using System.Runtime.ExceptionServices;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core;
using Nop.Core.Infrastructure;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Plugins;
using Nop.Plugin.Misc.GoogleBotAggregator.Services;
using Nop.Web.Framework.Infrastructure;

namespace Nop.Plugin.Misc.GoogleBotAggregator;

/// <summary>
/// Google Bot Aggregator plugin
/// </summary>
public class GoogleBotAggregatorPlugin : BasePlugin, IMiscPlugin
{
    #region Fields

    private readonly ISettingService _settingService;
    private readonly IWebHelper _webHelper;
    private readonly IGoogleBotService _googleBotService;
    private readonly ILocalizationService _localizationService;
    private readonly ILanguageService _languageService;
    private readonly IActionContextAccessor _actionContextAccessor;
    private readonly IUrlHelperFactory _urlHelperFactory;

    #endregion

    #region Ctor

    public GoogleBotAggregatorPlugin(
        ISettingService settingService,
        IWebHelper webHelper,
        IGoogleBotService googleBotService,
        ILocalizationService localizationService,
        ILanguageService languageService,
        IActionContextAccessor actionContextAccessor,
        IUrlHelperFactory urlHelperFactory)
    {
        _settingService = settingService;
        _webHelper = webHelper;
        _googleBotService = googleBotService;
        _localizationService = localizationService;
        _languageService = languageService;
        _actionContextAccessor = actionContextAccessor;
        _urlHelperFactory = urlHelperFactory;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Gets a configuration page URL
    /// </summary>
    public override string GetConfigurationPageUrl()
    {
         return $"{_webHelper.GetStoreLocation()}Admin/GoogleBotAggregator/Configure";
        //return _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext).RouteUrl(GoogleBotDefaults.ConfigurationRouteName);
    }

    /// <summary>
    /// Install plugin
    /// </summary>
    public override async Task InstallAsync()
    {
        try
        {
            //settings
            await _settingService.SaveSettingAsync(new GoogleBotAggregatorSettings());
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
           
        }
       

        //locales
        await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
        {
            ["Plugins.Misc.GoogleBotAggregator"] = "Google Bot Aggregator",
            ["Plugins.Misc.GoogleBotAggregator.ExcludeFromAnalytics"] = "Exclude from analytics",
            ["Plugins.Misc.GoogleBotAggregator.ExcludeFromAnalytics.Hint"] = "Check to exclude Google Bot traffic from analytics",
            ["Plugins.Misc.GoogleBotAggregator.MergeShoppingCarts"] = "Merge shopping carts",
            ["Plugins.Misc.GoogleBotAggregator.MergeShoppingCarts.Hint"] = "Check to merge shopping carts from Google Bot traffic",
            ["Plugins.Misc.GoogleBotAggregator.GoogleBotCustomerEmail"] = "Google Bot customer email",
            ["Plugins.Misc.GoogleBotAggregator.GoogleBotCustomerEmail.Hint"] = "Enter the email address for Google Bot customer account"
        });

        await base.InstallAsync();
    }

    /// <summary>
    /// Uninstall plugin
    /// </summary>
    public override async Task UninstallAsync()
    {
        //settings
        await _settingService.DeleteSettingAsync<GoogleBotAggregatorSettings>();

        //locales
        await _localizationService.DeleteLocaleResourcesAsync("Plugins.Misc.GoogleBotAggregator");

        await base.UninstallAsync();
    }

    #endregion
} 