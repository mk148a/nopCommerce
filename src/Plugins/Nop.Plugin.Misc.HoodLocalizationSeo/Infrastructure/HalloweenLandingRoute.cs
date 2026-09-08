using Nop.Core.Domain.Localization;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Infrastructure;

/// <summary>
/// Defines the one public Halloween landing route. Both the HTML head and the
/// XML sitemap use this helper so their language targets cannot drift apart.
/// </summary>
internal static class HalloweenLandingRoute
{
    internal const string Slug = "halloween-archery-and-costume-guide";

    private static readonly HashSet<string> TranslatedLanguageCodes = new(
        ["en", "tr", "de", "fr", "es"], StringComparer.OrdinalIgnoreCase);

    internal static string BuildPath(string languageCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(languageCode);

        return $"/{Uri.EscapeDataString(languageCode)}/{Slug}";
    }

    internal static IReadOnlyList<HalloweenLandingTarget> BuildTargets(Uri canonicalOrigin,
        IEnumerable<Language> languages,
        int defaultLanguageId)
    {
        ArgumentNullException.ThrowIfNull(canonicalOrigin);
        ArgumentNullException.ThrowIfNull(languages);

        var origin = canonicalOrigin.GetLeftPart(UriPartial.Path).TrimEnd('/');
        var targets = languages
            .Where(IsTranslatedLanguage)
            .DistinctBy(language => language.UniqueSeoCode, StringComparer.OrdinalIgnoreCase)
            .DistinctBy(language => language.LanguageCulture, StringComparer.OrdinalIgnoreCase)
            .Select(language => new HalloweenLandingTarget(language,
                $"{origin}{BuildPath(language.UniqueSeoCode)}", false))
            .ToList();

        if (targets.Count == 0)
            return targets;

        var defaultIndex = targets.FindIndex(target => target.Language.Id == defaultLanguageId);
        if (defaultIndex < 0)
            defaultIndex = targets.FindIndex(target => target.LanguageCode.Equals("en",
                StringComparison.OrdinalIgnoreCase));
        if (defaultIndex < 0)
            defaultIndex = 0;

        var defaultTarget = targets[defaultIndex] with { IsDefault = true };
        targets.RemoveAt(defaultIndex);
        targets.Insert(0, defaultTarget);
        return targets;
    }

    internal static bool IsTranslatedLanguage(Language language) =>
        language?.Published == true &&
        !string.IsNullOrWhiteSpace(language.UniqueSeoCode) &&
        !string.IsNullOrWhiteSpace(language.LanguageCulture) &&
        TranslatedLanguageCodes.Contains(language.UniqueSeoCode);
}

internal sealed record HalloweenLandingTarget(Language Language, string Url, bool IsDefault)
{
    internal string LanguageCode => Language.UniqueSeoCode;
    internal string LanguageCulture => Language.LanguageCulture;
}
