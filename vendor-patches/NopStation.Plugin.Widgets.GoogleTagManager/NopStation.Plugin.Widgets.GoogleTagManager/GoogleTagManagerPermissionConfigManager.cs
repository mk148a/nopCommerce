using System.Collections.Generic;
using Nop.Core.Domain.Customers;
using Nop.Services.Security;

namespace NopStation.Plugin.Widgets.GoogleTagManager;

public class GoogleTagManagerPermissionConfigManager : IPermissionConfigManager
{
	public IList<PermissionConfig> AllConfigs
	{
		get
		{
			//IL_0024: Unknown result type (might be due to invalid IL or missing references)
			//IL_002e: Expected O, but got Unknown
			//IL_004c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0056: Expected O, but got Unknown
			List<PermissionConfig> list = new List<PermissionConfig>();
			list.Add(new PermissionConfig("NopStation Google Tag Manager. Manage Configuration", "ManageNopStationGoogleTagManagerConfiguration", "NopStation", new string[1] { NopCustomerDefaults.AdministratorsRoleName }));
			list.Add(new PermissionConfig("NopStation Google Tag Manager. Manage ExportFile", "ManageNopStationGoogleTagManagerExportFile", "NopStation", new string[1] { NopCustomerDefaults.AdministratorsRoleName }));
			return list;
		}
	}
}
