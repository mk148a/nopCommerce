using Nop.Core;
using Nop.Core.Domain.Blogs;
using Nop.Core.Events;
using System.Text;
using Nop.Plugin.Misc.HoodLocalizationSeo.Services;
using Nop.Plugin.Misc.HoodLocalizationSeo.Infrastructure;
using Nop.Services.Blogs;
using Nop.Services.Catalog;
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
    private readonly IProductService _productService;
    private readonly IStoreContext _storeContext;
    private readonly ITopicService _topicService;
    private readonly IUrlRecordService _urlRecordService;
    private readonly IWebHelper _webHelper;

    public SitemapCreatedEventConsumer(IBlogLocalizationService blogLocalizationService,
        IBlogService blogService,
        ILanguageService languageService,
        IProductService productService,
        IStoreContext storeContext,
        ITopicService topicService,
        IUrlRecordService urlRecordService,
        IWebHelper webHelper)
    {
        _blogLocalizationService = blogLocalizationService;
        _blogService = blogService;
        _languageService = languageService;
        _productService = productService;
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
        if (!TryGetCanonicalStoreOrigin(store.Url, out var storeLocation))
        {
            // Without a configured canonical origin we cannot safely add or
            // classify generated routes. Preserve core's existing entries.
            return;
        }

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
                    returnDefaultValue: false, ensureTwoPublishedLanguages: false);
                if (!string.IsNullOrWhiteSpace(slug))
                    contactLocations.Add($"{storeLocation}/{language.UniqueSeoCode}/{slug}");
            }

            var contactCandidates = new List<SitemapUrlModel>();
            var languageCodes = languages.Select(language => language.UniqueSeoCode)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // ContactUs is the only route where customizations have historically
            // leaked foreign/protocol-relative locations into the sitemap. Keep
            // same-origin custom routes (for example /campaign/contactus), but
            // remove unsafe ContactUs locations while retaining a local canonical
            // location when one is present.
            foreach (var item in eventMessage.SitemapUrls.ToList())
                RemoveUnsafeContactLocations(item, storeLocation, eventMessage.SitemapUrls);

            foreach (var item in eventMessage.SitemapUrls.ToList())
                if (await IsContactLocationAsync(item, contactTopic.Id, contactLocations, storeLocation, languageCodes))
                    contactCandidates.Add(item);
            foreach (var existing in contactCandidates)
            {
                eventMessage.SitemapUrls.Remove(existing);
            }

            if (contactLocations.Count > 0)
            {
                eventMessage.SitemapUrls.Add(new SitemapUrlModel(contactLocations[0], contactLocations,
                    UpdateFrequency.Monthly, DateTime.UtcNow));
            }
        }

        var halloweenTargets = HalloweenLandingRoute.BuildTargets(new Uri(storeLocation + "/"),
            languages, store.DefaultLanguageId);
        var halloweenLocations = halloweenTargets.Select(target => target.Url).ToList();
        foreach (var existing in eventMessage.SitemapUrls
                     .Where(item => EnumerateLocations(item).Any(location =>
                         IsHalloweenLandingLocation(location, storeLocation))).ToList())
        {
            eventMessage.SitemapUrls.Remove(existing);
        }
        if (halloweenLocations.Count > 0)
        {
            eventMessage.SitemapUrls.Add(new SitemapUrlModel(halloweenLocations[0], halloweenLocations,
                UpdateFrequency.Monthly, DateTime.UtcNow));
        }

        var slugCache = new Dictionary<string, Nop.Core.Domain.Seo.UrlRecord>(StringComparer.OrdinalIgnoreCase);
        var productCache = new Dictionary<int, Nop.Core.Domain.Catalog.Product>();
        foreach (var existing in eventMessage.SitemapUrls.ToList())
        {
            if (await ContainsDeletedProductAsync(existing,
                    languages.Select(language => language.UniqueSeoCode), storeLocation, slugCache, productCache))
                eventMessage.SitemapUrls.Remove(existing);
        }

        NormalizeAndDeduplicate(eventMessage.SitemapUrls);
    }

    private static bool IsHalloweenLandingLocation(string location, string storeLocation)
    {
        if (!Uri.TryCreate(location, UriKind.Absolute, out var candidate) ||
            !Uri.TryCreate(storeLocation, UriKind.Absolute, out var store) ||
            !candidate.Scheme.Equals(store.Scheme, StringComparison.OrdinalIgnoreCase) ||
            !candidate.IdnHost.Equals(store.IdnHost, StringComparison.OrdinalIgnoreCase) ||
            candidate.Port != store.Port)
        {
            return false;
        }

        var basePath = store.AbsolutePath.Trim('/');
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

    private async Task<bool> ContainsDeletedProductAsync(SitemapUrlModel item,
        IEnumerable<string> languageCodes, string storeLocation,
        IDictionary<string, Nop.Core.Domain.Seo.UrlRecord> slugCache,
        IDictionary<int, Nop.Core.Domain.Catalog.Product> productCache)
    {
        var codes = languageCodes.Where(code => !string.IsNullOrWhiteSpace(code))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var location in EnumerateLocations(item))
        {
            if (!TryGetProductRouteSlug(location, storeLocation, codes, out var slug))
                continue;

            if (!slugCache.TryGetValue(slug, out var record))
            {
                record = await _urlRecordService.GetBySlugAsync(slug);
                slugCache[slug] = record;
            }
            if (record is null || !record.EntityName.Equals(nameof(Nop.Core.Domain.Catalog.Product),
                    StringComparison.OrdinalIgnoreCase))
                continue;

            if (!productCache.TryGetValue(record.EntityId, out var product))
            {
                product = await _productService.GetProductByIdAsync(record.EntityId);
                productCache[record.EntityId] = product;
            }
            if (product?.Deleted == true)
                return true;
        }

        return false;
    }

    private static bool TryGetProductRouteSlug(string url, string storeLocation,
        ISet<string> languageCodes, out string slug)
    {
        slug = string.Empty;
        var value = StripQueryAndFragment(url);
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var isAbsolute = Uri.TryCreate(value, UriKind.Absolute, out var absolute);
        var path = isAbsolute ? absolute.AbsolutePath : value;
        var basePath = Uri.TryCreate(storeLocation, UriKind.Absolute, out var storeUri)
            ? storeUri.AbsolutePath.Trim('/')
            : string.Empty;
        if (isAbsolute && (storeUri is null ||
            !absolute.Scheme.Equals(storeUri.Scheme, StringComparison.OrdinalIgnoreCase) ||
            !absolute.Host.Equals(storeUri.Host, StringComparison.OrdinalIgnoreCase) ||
            absolute.Port != storeUri.Port))
            return false;
        var normalizedPath = path.Trim('/');
        if (!string.IsNullOrEmpty(basePath))
        {
            if (!normalizedPath.StartsWith(basePath + "/", StringComparison.OrdinalIgnoreCase))
                return false;
            normalizedPath = normalizedPath[(basePath.Length + 1)..];
        }

        var segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        try
        {
            if (segments.Length == 2 && !languageCodes.Contains(Uri.UnescapeDataString(segments[0])))
                return false;
        }
        catch (UriFormatException)
        {
            return false;
        }
        if (segments.Length is < 1 or > 2)
            return false;

        var segment = segments[^1];
        if (string.IsNullOrWhiteSpace(segment))
            return false;

        try
        {
            slug = Uri.UnescapeDataString(segment);
            return !string.IsNullOrWhiteSpace(slug);
        }
        catch (UriFormatException)
        {
            return false;
        }
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
            contactLocations.Any(contact => UrlsEqual(candidate, contact)));
    }

    private async Task<bool> IsContactLocationAsync(SitemapUrlModel item, int contactTopicId,
        IReadOnlyCollection<string> contactLocations, string storeLocation, ISet<string> languageCodes)
    {
        if (IsContactLocation(item, contactLocations))
            return true;
        foreach (var location in EnumerateLocations(item))
        {
            if (!ContactSitemapRouteHelper.TryGetSlug(location, storeLocation, languageCodes, out var slug))
                continue;
            var record = await _urlRecordService.GetBySlugAsync(slug);
            if (record?.EntityId == contactTopicId && record.EntityName.Equals("Topic", StringComparison.OrdinalIgnoreCase))
                return true;
            if (record is null && ContactSitemapRouteHelper.IsLegacyContactRoute(location, storeLocation, languageCodes))
                return true;
        }
        return false;
    }

    private static IEnumerable<string> EnumerateLocations(SitemapUrlModel item)
    {
        if (!string.IsNullOrWhiteSpace(item?.Location))
            yield return item.Location;

        foreach (var alternate in item?.AlternateLocations ?? Array.Empty<string>())
            if (!string.IsNullOrWhiteSpace(alternate))
                yield return alternate;
    }

    private static void RemoveUnsafeContactLocations(SitemapUrlModel item, string storeLocation,
        IList<SitemapUrlModel> sitemapUrls)
    {
        if (item is null)
            return;

        var locations = EnumerateLocations(item).ToList();
        if (!locations.Any(location => IsUnsafeForeignContactLocation(location, storeLocation)))
            return;

        var safeLocations = locations.Where(location => !IsUnsafeForeignContactLocation(location, storeLocation)).ToList();
        if (safeLocations.Count == 0)
        {
            sitemapUrls.Remove(item);
            return;
        }

        item.Location = safeLocations[0];
        item.AlternateLocations = safeLocations;
    }

    private static bool IsUnsafeForeignContactLocation(string location, string storeLocation)
    {
        var value = (location ?? string.Empty).Split('?', '#')[0].Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var isProtocolRelative = value.StartsWith("//", StringComparison.Ordinal);
        var candidate = isProtocolRelative ? $"https:{value}" : value;
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
            return false;

        var pathSegments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (pathSegments.Length == 0 ||
            !Uri.UnescapeDataString(pathSegments[^1]).Equals("contactus", StringComparison.OrdinalIgnoreCase))
            return false;

        // Protocol-relative URLs are always unsafe here. Absolute URLs are
        // unsafe only when their origin differs from the configured store.
        if (isProtocolRelative)
            return true;

        return !TryGetCanonicalStoreOrigin(storeLocation, out var storeOrigin) ||
            !Uri.TryCreate(storeOrigin, UriKind.Absolute, out var storeUri) ||
            !uri.Scheme.Equals(storeUri.Scheme, StringComparison.OrdinalIgnoreCase) ||
            !uri.Host.Equals(storeUri.Host, StringComparison.OrdinalIgnoreCase) ||
            uri.Port != storeUri.Port;
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

    private static bool TryGetCanonicalStoreOrigin(string storeUrl, out string origin)
    {
        origin = string.Empty;
        if (!Uri.TryCreate(storeUrl?.Trim(), UriKind.Absolute, out var uri) ||
            !(uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
              uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) ||
            string.IsNullOrWhiteSpace(uri.Host) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
            return false;

        var builder = new UriBuilder(uri)
        {
            // Keep an explicitly configured non-default port. Uri semantics omit
            // default HTTP(S) ports while preserving custom ports such as :8443.
            Port = uri.IsDefaultPort ? -1 : uri.Port
        };
        origin = builder.Uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
        return !string.IsNullOrWhiteSpace(origin);
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
