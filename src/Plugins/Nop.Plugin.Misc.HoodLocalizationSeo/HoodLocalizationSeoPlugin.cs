using Nop.Plugin.Misc.HoodLocalizationSeo.Services;
using Nop.Services.Common;
using Nop.Services.Plugins;

namespace Nop.Plugin.Misc.HoodLocalizationSeo;

public sealed class HoodLocalizationSeoPlugin : BasePlugin, IMiscPlugin
{
    private readonly ILocalizationResourceInstaller _resourceInstaller;

    public HoodLocalizationSeoPlugin(ILocalizationResourceInstaller resourceInstaller)
    {
        _resourceInstaller = resourceInstaller;
    }

    public override async Task InstallAsync()
    {
        await _resourceInstaller.InstallEmbeddedPackageAsync();
        await base.InstallAsync();
    }

    public override async Task UpdateAsync(string currentVersion, string targetVersion)
    {
        await _resourceInstaller.InstallEmbeddedPackageAsync();
        await base.UpdateAsync(currentVersion, targetVersion);
    }
}
