using Nop.Services.Common;
using Nop.Services.Localization;
using Nop.Services.Plugins;

namespace Nop.Plugin.Misc.HoodLocalizationSeo;

public sealed class HoodLocalizationSeoPlugin : BasePlugin, IMiscPlugin
{
    private readonly ILocalizationService _localizationService;

    public HoodLocalizationSeoPlugin(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    public override async Task InstallAsync()
    {
        await UpsertOwnedResourcesAsync();
        await base.InstallAsync();
    }

    public override async Task UpdateAsync(string currentVersion, string targetVersion)
    {
        await UpsertOwnedResourcesAsync();
        await base.UpdateAsync(currentVersion, targetVersion);
    }

    private Task UpsertOwnedResourcesAsync()
    {
        // The deployment package may add language-specific values for these keys.
        // Supplying English defaults here makes a clean installation deterministic.
        return _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
        {
            ["Hood.LocalizationSeo.ContactCanonical"] = "Contact",
            ["Hood.LocalizationSeo.BlogFeedDescription"] = "Blog",
            ["Hood.LocalizationSeo.ResourcePackageVersion"] = "1.00"
        });
    }
}
