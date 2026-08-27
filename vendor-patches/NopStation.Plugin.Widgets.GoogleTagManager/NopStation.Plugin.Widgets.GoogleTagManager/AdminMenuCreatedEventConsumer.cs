using System.Linq;
using System.Threading.Tasks;
using Nop.Services.Events;
using Nop.Services.Localization;
using Nop.Services.Security;
using Nop.Web.Framework.Menu;
using NopStation.Plugin.Misc.Core.Infrastructure;

namespace NopStation.Plugin.Widgets.GoogleTagManager;

public class AdminMenuCreatedEventConsumer : IConsumer<AdminMenuEvent>
{
	private readonly ILocalizationService _localizationService;

	private readonly IPermissionService _permissionService;

	public AdminMenuCreatedEventConsumer(ILocalizationService localizationService, IPermissionService permissionService)
	{
		_localizationService = localizationService;
		_permissionService = permissionService;
	}

	public async Task HandleEventAsync(AdminMenuEvent createdEvent)
	{
		NopStationAdminMenuItem val = new NopStationAdminMenuItem();
		((AdminMenuItem)val).Visible = true;
		((AdminMenuItem)val).IconClass = "far fa-dot-circle";
		NopStationAdminMenuItem val2 = val;
		((AdminMenuItem)val2).Title = await _localizationService.GetResourceAsync("Admin.NopStation.GoogleTagManager.Menu.GoogleTagManager");
		NopStationAdminMenuItem menu = val;
		if (await _permissionService.AuthorizeAsync("ManageNopStationGoogleTagManagerConfiguration"))
		{
			AdminMenuItem val3 = new AdminMenuItem();
			AdminMenuItem val4 = val3;
			val4.Title = await _localizationService.GetResourceAsync("Admin.NopStation.GoogleTagManager.Menu.Configuration");
			val3.Url = "~/Admin/GoogleTagManager/Configure";
			val3.Visible = true;
			val3.IconClass = "far fa-circle";
			val3.SystemName = "GoogleTagManager.Configuration";
			((AdminMenuItem)menu).ChildNodes.Add(val3);
		}
		if (await _permissionService.AuthorizeAsync("ManageNopStationGoogleTagManagerExportFile"))
		{
			AdminMenuItem val4 = new AdminMenuItem();
			AdminMenuItem val3 = val4;
			val3.Title = await _localizationService.GetResourceAsync("Admin.NopStation.GoogleTagManager.Menu.ExportFile");
			val4.Url = "~/Admin/GoogleTagManager/ExportFile";
			val4.Visible = true;
			val4.IconClass = "far fa-circle";
			val4.SystemName = "GoogleTagManager.ExportFile";
			((AdminMenuItem)menu).ChildNodes.Add(val4);
		}
		if (((AdminMenuItem)menu).ChildNodes.Any())
		{
			if (await _permissionService.AuthorizeAsync("ShowNopStationDocumentations"))
			{
				AdminMenuItem val3 = new AdminMenuItem();
				AdminMenuItem val4 = val3;
				val4.Title = await _localizationService.GetResourceAsync("Admin.NopStation.Common.Menu.Documentation");
				val3.Url = "https://www.nop-station.com/google-tag-manager-documentation?utm_source=admin-panel?utm_source=admin-panel&utm_medium=gtm&utm_campaign=gtm";
				val3.Visible = true;
				val3.IconClass = "far fa-circle";
				val3.OpenUrlInNewTab = true;
				((AdminMenuItem)menu).ChildNodes.Add(val3);
			}
			createdEvent.PluginChildNodes.Add(menu);
		}
	}
}
