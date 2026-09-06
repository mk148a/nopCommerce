using System;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Infrastructure;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Web.Framework.Controllers;
using NopStation.Plugin.Misc.Core.Controllers;
using NopStation.Plugin.Misc.Core.Filters;
using NopStation.Plugin.Widgets.GoogleTagManager.Areas.Admin.Models;

namespace NopStation.Plugin.Widgets.GoogleTagManager.Areas.Admin.Controller;

public class GoogleTagManagerController : NopStationAdminController
{
	private readonly IStoreContext _storeContext;

	private readonly ILocalizationService _localizationService;

	private readonly INotificationService _notificationService;

	private readonly IPermissionService _permissionService;

	private readonly ISettingService _settingService;

	private readonly INopFileProvider _fileProvider;

	public GoogleTagManagerController(IStoreContext storeContext, ILocalizationService localizationService, INotificationService notificationService, IPermissionService permissionService, ISettingService settingService, INopFileProvider fileProvider)
	{
		_storeContext = storeContext;
		_localizationService = localizationService;
		_notificationService = notificationService;
		_permissionService = permissionService;
		_settingService = settingService;
		_fileProvider = fileProvider;
	}

	public async Task<IActionResult> Configure()
	{
		if (!(await _permissionService.AuthorizeAsync("ManageNopStationGoogleTagManagerConfiguration")))
		{
			return AccessDeniedView();
		}
		int storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
		GoogleTagManagerSettings googleTagManagerSettings = await _settingService.LoadSettingAsync<GoogleTagManagerSettings>(storeScope);
		ConfigurationModel model = new ConfigurationModel
		{
			IsEnable = googleTagManagerSettings.IsEnable,
			GTMContainerId = googleTagManagerSettings.GTMContainerId
		};
		model.ActiveStoreScopeConfiguration = storeScope;
		if (storeScope > 0)
		{
			ConfigurationModel configurationModel = model;
			configurationModel.IsEnable_OverrideForStore = await _settingService.SettingExistsAsync<GoogleTagManagerSettings, bool>(googleTagManagerSettings, (Expression<Func<GoogleTagManagerSettings, bool>>)((GoogleTagManagerSettings x) => x.IsEnable), storeScope);
			configurationModel = model;
			configurationModel.GTMContainerId_OverrideForStore = await _settingService.SettingExistsAsync<GoogleTagManagerSettings, string>(googleTagManagerSettings, (Expression<Func<GoogleTagManagerSettings, string>>)((GoogleTagManagerSettings x) => x.GTMContainerId), storeScope);
		}
		return ((Microsoft.AspNetCore.Mvc.Controller)(object)this).View("~/Plugins/NopStation.Plugin.Widgets.GoogleTagManager/Areas/Admin/Views/GoogleTagManager/Configure.cshtml", (object?)model);
	}

	[EditAccess(false)]
	[HttpPost]
	public async Task<IActionResult> Configure(ConfigurationModel model)
	{
		if (!(await _permissionService.AuthorizeAsync("ManageNopStationGoogleTagManagerConfiguration")))
		{
			return AccessDeniedView();
		}
		int storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
		GoogleTagManagerSettings googleTagManagerSettings = await _settingService.LoadSettingAsync<GoogleTagManagerSettings>(storeScope);
		googleTagManagerSettings.IsEnable = model.IsEnable;
		googleTagManagerSettings.GTMContainerId = model.GTMContainerId;
		await _settingService.SaveSettingOverridablePerStoreAsync<GoogleTagManagerSettings, bool>(googleTagManagerSettings, (Expression<Func<GoogleTagManagerSettings, bool>>)((GoogleTagManagerSettings x) => x.IsEnable), model.IsEnable_OverrideForStore, storeScope, false);
		await _settingService.SaveSettingOverridablePerStoreAsync<GoogleTagManagerSettings, string>(googleTagManagerSettings, (Expression<Func<GoogleTagManagerSettings, string>>)((GoogleTagManagerSettings x) => x.GTMContainerId), model.GTMContainerId_OverrideForStore, storeScope, false);
		await _settingService.ClearCacheAsync();
		INotificationService notificationService = _notificationService;
		notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Configuration.Updated"), true);
		return ((ControllerBase)(object)this).RedirectToAction("Configure");
	}

	public async Task<IActionResult> ExportFile()
	{
		if (!(await _permissionService.AuthorizeAsync("ManageNopStationGoogleTagManagerExportFile")))
		{
			return AccessDeniedView();
		}
		return ((Microsoft.AspNetCore.Mvc.Controller)(object)this).View();
	}

	[HttpPost]
	public async Task<IActionResult> ExportFile(FileInformationModel model)
	{
		if (!(await _permissionService.AuthorizeAsync("ManageNopStationGoogleTagManagerExportFile")))
		{
			return AccessDeniedView();
		}
		IActionResult result = default(IActionResult);
		object obj;
		int num;
		try
		{
			string text = _fileProvider.Combine(new string[2]
			{
				_fileProvider.MapPath("~/Plugins/NopStation.Plugin.Widgets.GoogleTagManager/"),
				"GTMPlugin.json"
			});
			string text2 = _fileProvider.ReadAllText(text, Encoding.UTF8);
			text2 = text2.Replace("%GOOGLEANALYTICSID%", model.GAContainerId);
			result = ((ControllerBase)(object)this).File(Encoding.UTF8.GetBytes(text2), MimeTypes.ApplicationJson, "GTMPlugin.json");
			return result;
		}
		catch (Exception ex)
		{
			obj = ex;
			num = 1;
		}
		if (num != 1)
		{
			return result;
		}
		Exception ex2 = (Exception)obj;
		await _notificationService.ErrorNotificationAsync(ex2, true);
		return ((ControllerBase)(object)this).RedirectToAction("ExportFile");
	}
}
