using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.Misc.HoodWebpOptimizer.Models;
using Nop.Plugin.Misc.HoodWebpOptimizer.Services;
using Nop.Services.Configuration;
using Nop.Services.Media;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Misc.HoodWebpOptimizer.Controllers;

[Area(AreaNames.ADMIN)]
[AuthorizeAdmin]
[AutoValidateAntiforgeryToken]
public sealed class HoodWebpOptimizerController : BasePluginController
{
    private readonly HoodWebpSettings _settings;
    private readonly ISettingService _settingService;
    private readonly IExistingPictureOptimizer _optimizer;
    private readonly IOriginalPictureStore _originals;
    private readonly IPictureService _pictureService;
    private readonly INotificationService _notificationService;

    public HoodWebpOptimizerController(HoodWebpSettings settings,
        ISettingService settingService,
        IExistingPictureOptimizer optimizer,
        IOriginalPictureStore originals,
        IPictureService pictureService,
        INotificationService notificationService)
    {
        _settings = settings;
        _settingService = settingService;
        _optimizer = optimizer;
        _originals = originals;
        _pictureService = pictureService;
        _notificationService = notificationService;
    }

    [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
    public IActionResult Configure() => View("~/Plugins/Misc.HoodWebpOptimizer/Views/HoodWebp/Configure.cshtml", ToModel());

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
    public async Task<IActionResult> Configure(ConfigureModel model)
    {
        _settings.Enabled = model.Enabled;
        _settings.PreserveOriginals = model.PreserveOriginals;
        _settings.MinimumSourceBytes = Math.Clamp(model.MinimumSourceBytes, 1024, 20 * 1024 * 1024);
        _settings.MinimumSavingsPercent = Math.Clamp(model.MinimumSavingsPercent, 1, 90);
        _settings.MinimumQuality = Math.Clamp(model.MinimumQuality, 45, 95);
        _settings.MaximumQuality = Math.Clamp(model.MaximumQuality, _settings.MinimumQuality, 100);
        _settings.MaximumPerceptualError = Math.Clamp(model.MaximumPerceptualError, 0.00005d, 0.02d);
        _settings.ProcessExistingPictures = model.ProcessExistingPictures;
        _settings.ExistingBatchSize = Math.Clamp(model.ExistingBatchSize, 1, 100);
        await _settingService.SaveSettingAsync(_settings);
        _notificationService.SuccessNotification("Adaptive WebP settings saved.");
        return RedirectToAction(nameof(Configure));
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
    public async Task<IActionResult> ProcessBatch()
    {
        if (_pictureService is not AdaptiveWebpPictureService)
        {
            _notificationService.WarningNotification("The active media provider is not local PictureService. This plugin has intentionally not replaced Azure Blob storage.");
            return RedirectToAction(nameof(Configure));
        }
        var count = await _optimizer.ProcessBatchAsync(_settings.ExistingBatchSize);
        _notificationService.SuccessNotification($"Processed {count} legacy image(s). Files without a safe WebP win were left unchanged.");
        return RedirectToAction(nameof(Configure));
    }

    [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
    public async Task<IActionResult> DownloadOriginal(int pictureId)
    {
        var original = await _originals.LoadAsync(pictureId);
        if (original is null)
            return NotFound();
        return File(original.Binary, original.MimeType, original.FileName);
    }

    private ConfigureModel ToModel() => new()
    {
        Enabled = _settings.Enabled,
        PreserveOriginals = _settings.PreserveOriginals,
        MinimumSourceBytes = _settings.MinimumSourceBytes,
        MinimumSavingsPercent = _settings.MinimumSavingsPercent,
        MinimumQuality = _settings.MinimumQuality,
        MaximumQuality = _settings.MaximumQuality,
        MaximumPerceptualError = _settings.MaximumPerceptualError,
        ProcessExistingPictures = _settings.ProcessExistingPictures,
        ExistingBatchSize = _settings.ExistingBatchSize,
        IsLocalPictureService = _pictureService is AdaptiveWebpPictureService
    };
}
