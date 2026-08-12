using Nop.Core;
using Nop.Core.Domain.Blogs;
using Nop.Core.Events;
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

            foreach (var existing in eventMessage.SitemapUrls.Where(item =>
                         item.Location?.Contains("/contactus", StringComparison.OrdinalIgnoreCase) == true ||
                         contactLocations.Any(location => UrlsEqual(item.Location, location))).ToList())
            {
                eventMessage.SitemapUrls.Remove(existing);
            }

            if (contactLocations.Count > 0)
            {
                eventMessage.SitemapUrls.Add(new SitemapUrlModel(contactLocations[0], contactLocations,
                    UpdateFrequency.Monthly, DateTime.UtcNow));
            }
        }

        // Defensive final deduplication by canonical location; keep the first
        // event-provided item so core frequency/lastmod data wins.
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var duplicate in eventMessage.SitemapUrls
                     .Where(item => !string.IsNullOrWhiteSpace(item.Location) && !seen.Add(Normalize(item.Location)))
                     .ToList())
        {
            eventMessage.SitemapUrls.Remove(duplicate);
        }
    }

    private static bool UrlMatchesAnySlug(SitemapUrlModel item, ISet<string> slugs)
    {
        return slugs.Any(slug => EndsWithSlug(item.Location, slug) ||
            item.AlternateLocations.Any(location => EndsWithSlug(location, slug)));
    }

    private static bool EndsWithSlug(string url, string slug)
    {
        return !string.IsNullOrWhiteSpace(url) && !string.IsNullOrWhiteSpace(slug) &&
               Normalize(url).EndsWith($"/{slug}", StringComparison.OrdinalIgnoreCase);
    }

    private static bool UrlsEqual(string left, string right) =>
        Normalize(left).Equals(Normalize(right), StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string url) =>
        (url ?? string.Empty).Split('?', '#')[0].TrimEnd('/');
}
