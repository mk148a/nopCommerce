# Hood PTT Post Service v25

Fixes the Configure page crash caused by a mixed deployment where `Views/Configure.cshtml` was updated to v24 but `Nop.Plugin.Shipping.FixedByWeightByTotal.dll` was still an older build.

## Root cause

`Configure.cshtml` referenced new PTT fields such as `HoodPttPostServiceEnabled`, but the runtime-loaded `ConfigurationModel` did not contain those properties. This means the v24 view was deployed without the matching rebuilt/published DLL.

## v25 changes

- Keeps the PTT settings UI, but reads PTT values through reflection so the Configure page does not crash during a mismatched deployment.
- Shows an admin warning if the loaded DLL does not contain the PTT fields.
- Keeps all v24 PTT live Post service code.

## Important

The guard prevents the Configure page from failing, but live PTT Post service works only when the v25 DLL is actually built and copied into the production plugin folder.

Production DLL to verify:

```text
C:\inetpub\wwwroot\hoodarcheryshop.com\Plugins\Shipping.FixedByWeightByTotal\Nop.Plugin.Shipping.FixedByWeightByTotal.dll
```

## Build

```powershell
dotnet build E:\Yazılım\nopCommerce\src\Plugins\Nop.Plugin.Shipping.FixedByWeightByTotal\Nop.Plugin.Shipping.FixedByWeightByTotal.csproj -c Release
iisreset
```
