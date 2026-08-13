using Nop.Core.Domain.Blogs;
using Nop.Core.Domain.Localization;
using Nop.Core.Domain.Stores;
using Nop.Services.Blogs;
using Nop.Services.Localization;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Services;

public sealed record BlogTagHreflangTarget(
    string LanguageCode,
    string LanguageCulture,
    string Tag,
    bool IsDefault);

public interface IBlogTagHreflangService
{
    Task<IReadOnlyList<BlogTagHreflangTarget>> GetTargetsAsync(
        string requestedTag,
        Language requestedLanguage,
        Store store);
}

/// <summary>
/// Resolves a localized blog-tag URL back to one source tag and projects that
/// same tag into every published language. Ambiguous localized labels fail
/// closed because one URL can represent the union of several source tags and
/// therefore has no exact cross-language equivalent.
/// </summary>
public sealed class BlogTagHreflangService : IBlogTagHreflangService
{
    private readonly IBlogLocalizationService _blogLocalizationService;
    private readonly IBlogService _blogService;
    private readonly ILanguageService _languageService;

    public BlogTagHreflangService(IBlogLocalizationService blogLocalizationService,
        IBlogService blogService,
        ILanguageService languageService)
    {
        _blogLocalizationService = blogLocalizationService;
        _blogService = blogService;
        _languageService = languageService;
    }

    public async Task<IReadOnlyList<BlogTagHreflangTarget>> GetTargetsAsync(
        string requestedTag,
        Language requestedLanguage,
        Store store)
    {
        if (string.IsNullOrWhiteSpace(requestedTag) || requestedLanguage is null || store is null)
            return Array.Empty<BlogTagHreflangTarget>();

        var sourceTags = (await _blogService.GetAllBlogPostTagsAsync(store.Id, store.DefaultLanguageId))
            .Select(tag => tag.Name?.Trim())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var matchingSources = await GetMatchingSourcesAsync(sourceTags, requestedTag, requestedLanguage.Id);

        if (matchingSources.Count != 1)
            return Array.Empty<BlogTagHreflangTarget>();

        var source = matchingSources[0];
        var languages = (await _languageService.GetAllLanguagesAsync(storeId: store.Id))
            .Where(language => language.Published &&
                               !string.IsNullOrWhiteSpace(language.UniqueSeoCode) &&
                               !string.IsNullOrWhiteSpace(language.LanguageCulture))
            .OrderBy(language => language.DisplayOrder)
            .ThenBy(language => language.Id)
            .ToList();

        if (languages.All(language => language.Id != store.DefaultLanguageId))
            return Array.Empty<BlogTagHreflangTarget>();

        var targets = new List<BlogTagHreflangTarget>(languages.Count);
        foreach (var language in languages)
        {
            var localizedLabel = await _blogLocalizationService.GetTagLabelAsync(source, language.Id);
            if (string.IsNullOrWhiteSpace(localizedLabel))
                return Array.Empty<BlogTagHreflangTarget>();

            // The localized blog page returns the union of every source tag
            // that has the requested label. A target is therefore equivalent
            // only when that label resolves back to this source and no other.
            // Validate the complete published-language cluster up front so
            // every emitted alternate can emit the same reciprocal cluster.
            var targetSources = await GetMatchingSourcesAsync(sourceTags, localizedLabel, language.Id);
            if (targetSources.Count != 1 ||
                !LabelsEqual(targetSources[0], source))
                return Array.Empty<BlogTagHreflangTarget>();

            targets.Add(new BlogTagHreflangTarget(
                language.UniqueSeoCode,
                language.LanguageCulture,
                localizedLabel.Trim(),
                language.Id == store.DefaultLanguageId));
        }

        return targets;
    }

    private async Task<List<string>> GetMatchingSourcesAsync(
        IEnumerable<string> sourceTags,
        string requestedLabel,
        int languageId)
    {
        if (string.IsNullOrWhiteSpace(requestedLabel))
            return [];

        var matchingSources = new List<string>();
        foreach (var sourceTag in sourceTags)
        {
            var localizedLabel = await _blogLocalizationService.GetTagLabelAsync(sourceTag, languageId);
            if (LabelsEqual(sourceTag, requestedLabel) ||
                LabelsEqual(localizedLabel, requestedLabel))
                matchingSources.Add(sourceTag);
        }

        return matchingSources;
    }

    private static bool LabelsEqual(string first, string second)
    {
        return !string.IsNullOrWhiteSpace(first) &&
               !string.IsNullOrWhiteSpace(second) &&
               first.Trim().Equals(second.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
