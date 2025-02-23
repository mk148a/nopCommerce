using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.Misc.GoogleBotAggregator.Models;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Misc.GoogleBotAggregator.Controllers;

[AutoValidateAntiforgeryToken]
[AuthorizeAdmin]
[Area(AreaNames.ADMIN)]
public class GoogleBotAggregatorController : BasePluginController
{
    #region Fields

    private readonly ISettingService _settingService;
    private readonly INotificationService _notificationService;
    private readonly ILocalizationService _localizationService;
    private readonly GoogleBotAggregatorSettings _googleBotAggregatorSettings;
    private readonly IPermissionService _permissionService;

    #endregion

    #region Ctor

    public GoogleBotAggregatorController(
        ISettingService settingService,
        INotificationService notificationService,
        ILocalizationService localizationService,
        GoogleBotAggregatorSettings googleBotAggregatorSettings,
        IPermissionService permissionService)
    {
        _settingService = settingService;
        _notificationService = notificationService;
        _localizationService = localizationService;
        _googleBotAggregatorSettings = googleBotAggregatorSettings;
        _permissionService = permissionService;
    }

    #endregion

    #region Methods

    
    public IActionResult Configure()
    {
        var model = new ConfigurationModel
        {
            ExcludeFromAnalytics = _googleBotAggregatorSettings.ExcludeFromAnalytics,
            MergeShoppingCarts = _googleBotAggregatorSettings.MergeShoppingCarts,
            GoogleBotCustomerEmail = _googleBotAggregatorSettings.GoogleBotCustomerEmail
        };

        return View("~/Plugins/Misc.GoogleBotAggregator/Views/Configure.cshtml", model);
    }

    [HttpPost]
    public async Task<IActionResult> Configure(ConfigurationModel model)
    {
        if (!ModelState.IsValid)
            return await Task.FromResult<IActionResult>(Configure());

        _googleBotAggregatorSettings.ExcludeFromAnalytics = model.ExcludeFromAnalytics;
        _googleBotAggregatorSettings.MergeShoppingCarts = model.MergeShoppingCarts;
        _googleBotAggregatorSettings.GoogleBotCustomerEmail = model.GoogleBotCustomerEmail;

        await _settingService.SaveSettingAsync(_googleBotAggregatorSettings);

        _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

        return await Task.FromResult<IActionResult>(Configure());
    }

    #endregion
} 