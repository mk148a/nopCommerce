using Nop.Core;
using Nop.Core.Domain.Blogs;
using Nop.Plugin.Misc.HoodLocalizationSeo.Services;
using Nop.Services.Blogs;
using Nop.Services.Localization;
using Nop.Services.Seo;
using Nop.Services.Topics;
using Nop.Web.Factories;
using Nop.Web.Models.Sitemap;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Factories;

/// <summary>
/// Post-processes the public HTML sitemap while delegating XML generation to
/// core. XML URL correction is performed at the SitemapCreatedEvent extension
/// point before core serializes the file.
/// </summary>
public sealed class LocalizedSitemapModelFactory : ISitemapModelFactory
{
    private readonly ISitemapModelFactory _inner;
    private readonly IBlogLocalizationService _blogLocalizationService;
    private readonly IBlogService _blogService;
    private readonly IStoreContext _storeContext;
    private readonly ITopicService _topicService;
    private readonly IUrlRecordService _urlRecordService;
    private readonly ILanguageService _languageService;
    private readonly IWorkContext _workContext;

    public LocalizedSitemapModelFactory(ISitemapModelFactory inner,
        IBlogLocalizationService blogLocalizationService,
        IBlogService blogService,
        IStoreContext storeContext,
        ITopicService topicService,
        IUrlRecordService urlRecordService,
        IWorkContext workContext,
        ILanguageService languageService)
    {
        _inner = inner;
        _blogLocalizationService = blogLocalizationService;
        _blogService = blogService;
        _storeContext = storeContext;
        _topicService = topicService;
        _urlRecordService = urlRecordService;
        _workContext = workContext;
        _languageService = languageService;
    }

    public async Task<SitemapModel> PrepareSitemapModelAsync(SitemapPageModel pageModel)
    {
        var model = await _inner.PrepareSitemapModelAsync(pageModel);
        var store = await _storeContext.GetCurrentStoreAsync();

        // Store.Url is the only trusted origin for HTML sitemap rewrites. If it
        // is empty or malformed, return the inner model untouched rather than
        // deriving an origin from the current request via IWebHelper.
        if (store is null || !TryGetCanonicalStoreOrigin(store.Url, out var canonicalOrigin))
            return model;

        var language = await _workContext.GetWorkingLanguageAsync();
        var storeLocation = canonicalOrigin.ToString().TrimEnd('/');
        var languageCodes = (await _languageService.GetAllLanguagesAsync(storeId: store.Id))
            .Where(candidate => candidate.Published)
            .Select(candidate => candidate.UniqueSeoCode)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var posts = (await _blogService.GetAllBlogPostsAsync(store.Id, store.DefaultLanguageId))
            .Where(post => post.IncludeInSitemap)
            .ToList();

        foreach (var post in posts)
        {
            var defaultSlug = await _urlRecordService.GetSeNameAsync(post.Id, nameof(BlogPost), post.LanguageId,
                returnDefaultValue: true, ensureTwoPublishedLanguages: false);
            var localizedSlug = await _blogLocalizationService.GetSlugAsync(post, language.Id);
            var item = model.Items.FirstOrDefault(candidate =>
                UrlEndsWithSlug(candidate.Url, defaultSlug) || UrlEndsWithSlug(candidate.Url, localizedSlug));
            if (item is null)
                continue;

            item.Name = await _blogLocalizationService.GetFieldAsync(post, nameof(BlogPost.Title), language.Id, post.Title);
            item.Url = BuildLocalizedPath(canonicalOrigin, language.UniqueSeoCode, localizedSlug);
        }

        var contactTopic = await _topicService.GetTopicBySystemNameAsync("ContactUs", store.Id);
        if (contactTopic is not null)
        {
            var contactSlug = await _urlRecordService.GetSeNameAsync(contactTopic.Id, "Topic", language.Id,
                returnDefaultValue: false, ensureTwoPublishedLanguages: false);
            var fallbackContactSlug = string.Empty;
            if (string.IsNullOrWhiteSpace(contactSlug))
            {
                fallbackContactSlug = await _urlRecordService.GetSeNameAsync(contactTopic.Id, "Topic", 0,
                    returnDefaultValue: true, ensureTwoPublishedLanguages: false);
            }

            var contactCandidateSlugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "contactus" };
            if (!string.IsNullOrWhiteSpace(contactSlug))
                contactCandidateSlugs.Add(contactSlug);
            if (!string.IsNullOrWhiteSpace(fallbackContactSlug))
                contactCandidateSlugs.Add(fallbackContactSlug);

            var contactCandidates = new List<SitemapModel.SitemapItemModel>();
            var verifiedContactCandidates = new List<SitemapModel.SitemapItemModel>();
            foreach (var item in model.Items.ToList())
            {
                if (!ContactSitemapRouteHelper.TryGetSlug(item.Url, storeLocation, languageCodes, out var slug) ||
                    !contactCandidateSlugs.Contains(slug))
                    continue;

                var record = await _urlRecordService.GetBySlugAsync(slug);
                if (record?.EntityId == contactTopic.Id && string.Equals(record.EntityName, "Topic", StringComparison.OrdinalIgnoreCase))
                {
                    contactCandidates.Add(item);
                    verifiedContactCandidates.Add(item);
                }
                else if (record is null && ContactSitemapRouteHelper.IsLegacyContactRoute(item.Url, storeLocation, languageCodes))
                    contactCandidates.Add(item);
            }
            if (contactCandidates.Count > 0)
            {
                if (string.IsNullOrWhiteSpace(contactSlug))
                {
                    foreach (var candidate in verifiedContactCandidates)
                        model.Items.Remove(candidate);
                }
                else
                {
                    contactCandidates[0].Url = BuildLocalizedPath(canonicalOrigin, language.UniqueSeoCode, contactSlug);
                    foreach (var duplicate in contactCandidates.Skip(1))
                        model.Items.Remove(duplicate);
                }
            }
        }

        return model;
    }

    public Task<SitemapXmlModel> PrepareSitemapXmlModelAsync(int id = 0)
    {
        return _inner.PrepareSitemapXmlModelAsync(id);
    }

    public Task<SitemapUrlModel> PrepareLocalizedSitemapUrlAsync(string routeName,
        Func<int?, Task<object>> getRouteParamsAwait = null,
        DateTime? dateTimeUpdatedOn = null,
        UpdateFrequency updateFreq = UpdateFrequency.Weekly)
    {
        // This member is part of the 4.80 interface and was removed in 4.90.
        // Invoke it reflectively so the same source remains compilable after
        // retargeting the plugin project to a newer nopCommerce checkout.
        var method = _inner.GetType().GetMethod(nameof(PrepareLocalizedSitemapUrlAsync),
            new[] { typeof(string), typeof(Func<int?, Task<object>>), typeof(DateTime?), typeof(UpdateFrequency) })
            ?? throw new MissingMethodException(_inner.GetType().FullName,
                nameof(PrepareLocalizedSitemapUrlAsync));
        return (Task<SitemapUrlModel>)method.Invoke(_inner,
            new object[] { routeName, getRouteParamsAwait, dateTimeUpdatedOn, updateFreq });
    }

    private static string BuildLocalizedPath(Uri canonicalOrigin, string languageCode, string slug)
    {
        var basePath = canonicalOrigin.AbsolutePath.TrimEnd('/');
        return $"{basePath}/{languageCode}/{slug}";
    }

    private static bool UrlEndsWithSlug(string url, string slug)
    {
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(slug))
            return false;

        return Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out var uri)
            && (uri.IsAbsoluteUri ? uri.AbsolutePath : url.Split('?', '#')[0])
                .TrimEnd('/').EndsWith($"/{slug}", StringComparison.OrdinalIgnoreCase);
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
            return false;

        var canonicalValue = storeUri.GetLeftPart(UriPartial.Path).TrimEnd('/');
        return Uri.TryCreate(canonicalValue,
            UriKind.Absolute, out canonicalOrigin);
    }
}
