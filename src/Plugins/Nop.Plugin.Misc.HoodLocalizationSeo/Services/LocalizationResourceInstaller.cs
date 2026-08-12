using Nop.Services.Localization;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Services;

/// <summary>
/// Stable hook used by deployment/migration code to seed resource values by
/// nopCommerce language id. It intentionally does not embed machine-generated
/// translations in the plugin assembly.
/// </summary>
public interface ILocalizationResourceInstaller
{
    Task UpsertAsync(IReadOnlyDictionary<int, IReadOnlyDictionary<string, string>> resourcesByLanguageId);
}

public sealed class LocalizationResourceInstaller : ILocalizationResourceInstaller
{
    private readonly ILocalizationService _localizationService;

    public LocalizationResourceInstaller(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    public async Task UpsertAsync(IReadOnlyDictionary<int, IReadOnlyDictionary<string, string>> resourcesByLanguageId)
    {
        ArgumentNullException.ThrowIfNull(resourcesByLanguageId);

        foreach (var (languageId, resources) in resourcesByLanguageId)
        {
            if (languageId <= 0 || resources.Count == 0)
                continue;

            await _localizationService.AddOrUpdateLocaleResourceAsync(
                resources.ToDictionary(pair => pair.Key, pair => pair.Value), languageId);
        }
    }
}
