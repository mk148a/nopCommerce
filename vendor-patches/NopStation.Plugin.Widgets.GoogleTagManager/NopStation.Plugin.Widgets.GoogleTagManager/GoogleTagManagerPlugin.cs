using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Nop.Core;
using Nop.Services.Cms;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Plugins;
using Nop.Services.Security;
using Nop.Web.Framework.Infrastructure;
using NopStation.Plugin.Misc.Core.Services;
using NopStation.Plugin.Widgets.GoogleTagManager.Components;

namespace NopStation.Plugin.Widgets.GoogleTagManager;

public class GoogleTagManagerPlugin : BasePlugin, IWidgetPlugin, IPlugin, INopStationPlugin
{
	private readonly ILocalizationService _localizationService;

	private readonly ISettingService _settingService;

	private readonly IWebHelper _webHelper;

	private readonly IPermissionService _permissionService;

	public bool HideInWidgetList => false;

	public GoogleTagManagerPlugin(ILocalizationService localizationService, ISettingService settingService, IPermissionService permissionService, IWebHelper webHelper)
	{
		_localizationService = localizationService;
		_settingService = settingService;
		_permissionService = permissionService;
		_webHelper = webHelper;
	}

	public Task<IList<string>> GetWidgetZonesAsync()
	{
		return Task.FromResult((IList<string>)new List<string>
		{
			PublicWidgetZones.HeadHtmlTag,
			PublicWidgetZones.BodyStartHtmlTagAfter
		});
	}

	public override string GetConfigurationPageUrl()
	{
		return _webHelper.GetStoreLocation((bool?)null) + "Admin/GoogleTagManager/Configure";
	}

	public Type GetWidgetViewComponent(string widgetZone)
	{
		return typeof(GoogleTagManagerViewComponent);
	}

	public override async Task InstallAsync()
	{
		GoogleTagManagerSettings googleTagManagerSettings = new GoogleTagManagerSettings
		{
			GTMContainerId = "GTM-XXXXXX"
		};
		await _settingService.SaveSettingAsync<GoogleTagManagerSettings>(googleTagManagerSettings, 0);
		await NopStationHelpers.InstallPluginAsync<GoogleTagManagerPlugin>(this, true);
		await _003C_003En__0();
	}

	public override async Task UninstallAsync()
	{
		await NopStationHelpers.UninstallPluginAsync<GoogleTagManagerPlugin>(this, (IPermissionConfigManager)(object)new GoogleTagManagerPermissionConfigManager());
		await _003C_003En__1();
	}

	public IDictionary<string, string> GetPluginResources()
	{
		return new Dictionary<string, string>
		{
			["Admin.NopStation.GoogleTagManager.Menu.GoogleTagManager"] = "Google Tag Manager",
			["Admin.NopStation.GoogleTagManager.Menu.Configuration"] = "Configuration",
			["Admin.NopStation.GoogleTagManager.Menu.ExportFile"] = "Export File",
			["Admin.NopStation.GoogleTagManager.Configuration.Fields.IsEnable"] = "Enable plugin",
			["Admin.NopStation.GoogleTagManager.Configuration.Fields.IsEnable.Hint"] = "Enable this plugin.",
			["Admin.NopStation.GoogleTagManager.Configuration.Fields.GTMContainerId"] = "Google Tag Manager Id",
			["Admin.NopStation.GoogleTagManager.Configuration.Fields.GTMContainerId.Hint"] = "Give Google Tag Manager Container Id such as GTM-XXXXXX",
			["Admin.NopStation.GoogleTagManager.ExportFileInformation.Fields.GAContainerId"] = "Google analytics ID",
			["Admin.NopStation.GoogleTagManager.ExportFileInformation.Fields.GAContainerId.Hint"] = "Give Google analytics 4 ID.",
			["Admin.NopStation.GoogleTagManager.Configuration.GTMContainerId.Required"] = "This field is required.",
			["Admin.NopStation.GoogleTagManager.Configuration"] = "Google Tag Manager settings",
			["Admin.NopStation.GoogleTagManager.ExportFile"] = "Google Tag Manager Export File"
		};
	}

	[CompilerGenerated]
	[DebuggerHidden]
	private Task _003C_003En__0()
	{
		return ((BasePlugin)this).InstallAsync();
	}

	[CompilerGenerated]
	[DebuggerHidden]
	private Task _003C_003En__1()
	{
		return ((BasePlugin)this).UninstallAsync();
	}
}
