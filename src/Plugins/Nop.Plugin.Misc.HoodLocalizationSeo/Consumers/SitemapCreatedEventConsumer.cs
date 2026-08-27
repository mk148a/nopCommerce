using Nop.Core;
using Nop.Core.Domain.Blogs;
using Nop.Core.Events;
using System.Text;
using Nop.Plugin.Misc.HoodLocalizationSeo.Infrastructure;
using Nop.Plugin.Misc.HoodLocalizationSeo.Services;
using Nop.Services.Blogs;
using Nop.Services.Events;
using Nop.Services.Localization;
using Nop.Services.Seo;
using Nop.Services.Topics;
using Nop.Web.Models.Sitemap;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Consumers;

public sealed class SitemapCreatedEventConsumer : IConsumer<SitemapCreatedEvent>
{
    private readonly IBlogLocalizationService _blogLocalizationService;
    private readonly IBlogService _blogService;
    private readonly ILanguageService _languageService;
    private readonly IStoreContext _storeContext;
    private readonly ITopicService _topicService;
    private readonly IUrlRecordService _urlRecordService;
    private readonly IWebHelper _webHelper;

    public SitemapCreatedEventConsumer(IBlogLocalizationService blogLocalizationService,
        IBlogService blogService,
        ILanguageService languageService,
        IStoreContext storeContext,
        ITopicService topicService,
        IUrlRecordService urlRecordService,
        IWebHelper webHelper)
    {
        _blogLocalizationService = blogLocalizationService;
        _blogService = blogService;
        _languageService = languageService;
        _storeContext = storeContext;
        _topicService = topicService;
        _urlRecordService = urlRecordService;
        _webHelper = webHelper;
    }

    public async Task HandleEventAsync(SitemapCreatedEvent eventMessage)
    {
        var store = await _storeContext.GetCurrentStoreAsync();
        var languages = (await _languageService.GetAllLanguagesAsync(storeId: store.Id))
            .Where(language => language.Published).ToList();
        var storeLocation = _webHelper.GetStoreLocation().TrimEnd('/');

        var posts = (await _blogService.GetAllBlogPostsAsync(store.Id, store.DefaultLanguageId))
            .Where(post => post.IncludeInSitemap).ToList();
        foreach (var post in posts)
        {
            var knownSlugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var language in languages)
            {
                knownSlugs.Add(await _blogLocalizationService.GetSlugAsync(post, language.Id));
            }

            foreach (var existing in eventMessage.SitemapUrls
                         .Where(item => UrlMatchesAnySlug(item, knownSlugs)).ToList())
            {
                eventMessage.SitemapUrls.Remove(existing);
            }

            var locations = new List<string>();
            foreach (var language in languages)
            {
                var slug = await _blogLocalizationService.GetSlugAsync(post, language.Id);
                if (!string.IsNullOrWhiteSpace(slug))
                    locations.Add($"{storeLocation}/{language.UniqueSeoCode}/{slug}");
            }

            if (locations.Count > 0)
            {
                eventMessage.SitemapUrls.Add(new SitemapUrlModel(locations[0], locations,
                    UpdateFrequency.Weekly, post.CreatedOnUtc));
            }
        }

        var contactTopic = await _topicService.GetTopicBySystemNameAsync("ContactUs", store.Id);
        if (contactTopic is not null)
        {
            var contactLocations = new List<string>();
            foreach (var language in languages)
            {
                var slug = await _urlRecordService.GetSeNameAsync(contactTopic.Id, "Topic", language.Id,
                    returnDefaultValue: true, ensureTwoPublishedLanguages: false);
                if (!string.IsNullOrWhiteSpace(slug))
                    contactLocations.Add($"{storeLocation}/{language.UniqueSeoCode}/{slug}");
            }

            foreach (var existing in eventMessage.SitemapUrls
                         .Where(item => IsContactLocation(item, contactLocations)).ToList())
            {
                eventMessage.SitemapUrls.Remove(existing);
            }

            if (contactLocations.Count > 0)
            {
                eventMessage.SitemapUrls.Add(new SitemapUrlModel(contactLocations[0], contactLocations,
                    UpdateFrequency.Monthly, DateTime.UtcNow));
            }
        }

        if (TryGetCanonicalStoreOrigin(store.Url, out var canonicalOrigin))
        {
            var halloweenTargets = HalloweenLandingRoute.BuildTargets(canonicalOrigin,
                languages, store.DefaultLanguageId);
            var halloweenLocations = halloweenTargets.Select(target => target.Url).ToList();
            foreach (var existing in eventMessage.SitemapUrls
                         .Where(item => EnumerateLocations(item).Any(location =>
                             IsHalloweenLandingLocation(location, canonicalOrigin))).ToList())
            {
                eventMessage.SitemapUrls.Remove(existing);
            }

            if (halloweenLocations.Count > 0)
            {
                eventMessage.SitemapUrls.Add(new SitemapUrlModel(halloweenLocations[0], halloweenLocations,
                    UpdateFrequency.Monthly, DateTime.UtcNow));
            }
        }

        NormalizeAndDeduplicate(eventMessage.SitemapUrls);
    }

    private static bool IsHalloweenLandingLocation(string location, Uri canonicalOrigin)
    {
        if (!Uri.TryCreate(location, UriKind.Absolute, out var candidate) ||
            !candidate.Scheme.Equals(canonicalOrigin.Scheme, StringComparison.OrdinalIgnoreCase) ||
            !candidate.IdnHost.Equals(canonicalOrigin.IdnHost, StringComparison.OrdinalIgnoreCase) ||
            candidate.Port != canonicalOrigin.Port)
        {
            return false;
        }

        var basePath = canonicalOrigin.AbsolutePath.Trim('/');
        var candidatePath = candidate.AbsolutePath.Trim('/');
        if (!string.IsNullOrEmpty(basePath))
        {
            if (!candidatePath.StartsWith(basePath + "/", StringComparison.OrdinalIgnoreCase))
                return false;
            candidatePath = candidatePath[(basePath.Length + 1)..];
        }

        var segments = candidatePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 2)
            return false;

        try
        {
            return Uri.UnescapeDataString(segments[1]).Equals(HalloweenLandingRoute.Slug,
                StringComparison.OrdinalIgnoreCase);
        }
        catch (UriFormatException)
        {
            return false;
        }
    }

    private static bool TryGetCanonicalStoreOrigin(string storeUrl, out Uri canonicalOrigin)
    {
        canonicalOrigin = null;
        if (!Uri.TryCreate(storeUrl, UriKind.Absolute, out var storeUri) ||
            (storeUri.Scheme != Uri.UriSchemeHttp && storeUri.Scheme != Uri.UriSchemeHttps) ||
            string.IsNullOrWhiteSpace(storeUri.Host) ||
            !string.IsNullOrEmpty(storeUri.UserInfo) ||
            !string.IsNullOrEmpty(storeUri.Query) ||
            !string.IsNullOrEmpty(storeUri.Fragment))
        {
            return false;
        }

        return Uri.TryCreate(storeUri.GetLeftPart(UriPartial.Path).TrimEnd('/') + "/",
            UriKind.Absolute, out canonicalOrigin);
    }

    private static bool UrlMatchesAnySlug(SitemapUrlModel item, ISet<string> slugs)
    {
        return slugs.Any(slug => EndsWithSlug(item.Location, slug) ||
            (item.AlternateLocations ?? Array.Empty<string>())
            .Any(location => EndsWithSlug(location, slug)));
    }

    private static bool EndsWithSlug(string url, string slug)
    {
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(slug))
            return false;

        var path = GetSemanticPath(url);
        var slugPath = GetSemanticPath(slug);
        return !string.IsNullOrEmpty(path) && !string.IsNullOrEmpty(slugPath) &&
               (path.Equals(slugPath, StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith($"/{slugPath}", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsContactLocation(SitemapUrlModel item, IReadOnlyCollection<string> contactLocations)
    {
        return EnumerateLocations(item).Any(candidate =>
            EndsWithSlug(candidate, "contactus") ||
            contactLocations.Any(contact => UrlsEqual(candidate, contact)));
    }

    private static IEnumerable<string> EnumerateLocations(SitemapUrlModel item)
    {
        if (!string.IsNullOrWhiteSpace(item?.Location))
            yield return item.Location;

        foreach (var alternate in item?.AlternateLocations ?? Array.Empty<string>())
            if (!string.IsNullOrWhiteSpace(alternate))
                yield return alternate;
    }

    private static void NormalizeAndDeduplicate(IList<SitemapUrlModel> sitemapUrls)
    {
        foreach (var item in sitemapUrls)
            AlignLocationWithAlternates(item);

        // Keep the first model's update metadata, but merge any distinct
        // hreflang alternates before removing a semantic duplicate.
        var firstByLocation = new Dictionary<string, SitemapUrlModel>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in sitemapUrls.ToList())
        {
            var key = Normalize(item.Location);
            if (string.IsNullOrEmpty(key))
                continue;

            if (!firstByLocation.TryGetValue(key, out var first))
            {
                firstByLocation[key] = item;
                continue;
            }

            MergeAlternates(first, item.AlternateLocations);
            AlignLocationWithAlternates(first);
            sitemapUrls.Remove(item);
        }
    }

    private static void MergeAlternates(SitemapUrlModel target, IEnumerable<string> additions)
    {
        var merged = (target.AlternateLocations ?? Array.Empty<string>()).ToList();
        merged.AddRange(additions ?? Array.Empty<string>());
        target.AlternateLocations = merged;
    }

    private static void AlignLocationWithAlternates(SitemapUrlModel item)
    {
        if (item is null)
            return;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var uniqueAlternates = new List<string>();
        foreach (var alternate in item.AlternateLocations ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(alternate))
                continue;

            var key = Normalize(alternate);
            if (string.IsNullOrEmpty(key) || seen.Add(key))
                uniqueAlternates.Add(alternate);
        }

        item.AlternateLocations = uniqueAlternates;
        var exactRepresentative = uniqueAlternates.FirstOrDefault(alternate =>
            UrlsEqual(item.Location, alternate));
        if (exactRepresentative is not null)
            item.Location = exactRepresentative;
    }

    private static bool UrlsEqual(string left, string right)
    {
        var normalizedLeft = Normalize(left);
        var normalizedRight = Normalize(right);
        return !string.IsNullOrEmpty(normalizedLeft) &&
               normalizedLeft.Equals(normalizedRight, StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string url)
    {
        var value = StripQueryAndFragment(url);
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute) &&
            (absolute.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
             absolute.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            var origin = absolute.GetComponents(UriComponents.SchemeAndServer, UriFormat.SafeUnescaped)
                .Normalize(NormalizationForm.FormC);
            return $"absolute|{origin}|{GetNormalizedPath(absolute)}";
        }

        var rooted = value.StartsWith("/", StringComparison.Ordinal);
        return $"{(rooted ? "root" : "relative")}|{GetSemanticPath(value)}";
    }

    private static string GetSemanticPath(string value)
    {
        var stripped = StripQueryAndFragment(value);
        if (string.IsNullOrEmpty(stripped))
            return string.Empty;

        if (Uri.TryCreate(stripped, UriKind.Absolute, out var absolute))
            return GetNormalizedPath(absolute);

        if (Uri.TryCreate(new Uri("https://semantic.invalid/"), stripped.TrimStart('/'), out var relative))
            return GetNormalizedPath(relative);

        return stripped.Trim('/').Normalize(NormalizationForm.FormC);
    }

    private static string GetNormalizedPath(Uri uri) =>
        uri.GetComponents(UriComponents.Path, UriFormat.SafeUnescaped)
            .Normalize(NormalizationForm.FormC)
            .TrimEnd('/');

    private static string StripQueryAndFragment(string url) =>
        (url ?? string.Empty).Split('?', '#')[0].Trim().TrimEnd('/');
}
