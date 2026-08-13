using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Blogs;
using Nop.Core.Domain.Localization;
using Nop.Core.Rss;
using Nop.Plugin.Misc.HoodLocalizationSeo.Services;
using Nop.Services.Blogs;
using Nop.Services.Localization;
using Nop.Services.Seo;
using Nop.Services.Topics;
using Nop.Web.Controllers;
using Nop.Web.Factories;
using Nop.Web.Framework.Mvc;
using Nop.Web.Framework.Mvc.Filters;
using Nop.Web.Models.Common;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Controllers;

public sealed class HoodLocalizationController : BasePublicController
{
    private readonly BlogSettings _blogSettings;
    private readonly IBlogLocalizationService _blogLocalizationService;
    private readonly IBlogService _blogService;
    private readonly ICommonModelFactory _commonModelFactory;
    private readonly ILanguageService _languageService;
    private readonly ILocalizationService _localizationService;
    private readonly IStoreContext _storeContext;
    private readonly ITopicService _topicService;
    private readonly IUrlRecordService _urlRecordService;
    private readonly IWebHelper _webHelper;

    public HoodLocalizationController(BlogSettings blogSettings,
        IBlogLocalizationService blogLocalizationService,
        IBlogService blogService,
        ICommonModelFactory commonModelFactory,
        ILanguageService languageService,
        ILocalizationService localizationService,
        IStoreContext storeContext,
        ITopicService topicService,
        IUrlRecordService urlRecordService,
        IWebHelper webHelper)
    {
        _blogSettings = blogSettings;
        _blogLocalizationService = blogLocalizationService;
        _blogService = blogService;
        _commonModelFactory = commonModelFactory;
        _languageService = languageService;
        _localizationService = localizationService;
        _storeContext = storeContext;
        _topicService = topicService;
        _urlRecordService = urlRecordService;
        _webHelper = webHelper;
    }

    [HttpGet]
    [CheckLanguageSeoCode(ignore: true)]
    public async Task<IActionResult> BlogRss(int languageId)
    {
        var store = await _storeContext.GetCurrentStoreAsync();
        var language = await _languageService.GetLanguageByIdAsync(languageId)
            ?? await _languageService.GetLanguageByIdAsync(store.DefaultLanguageId);
        languageId = language?.Id ?? store.DefaultLanguageId;

        var storeName = await _localizationService.GetLocalizedAsync(store, entity => entity.Name,
            languageId, ensureTwoPublishedLanguages: false);
        var blogLabel = await _localizationService.GetResourceAsync("Blog", languageId,
            logIfNotFound: false, defaultValue: "Blog");
        var feed = new RssFeed($"{storeName}: {blogLabel}", blogLabel,
            new Uri(_webHelper.GetStoreLocation()), DateTime.UtcNow);

        if (_blogSettings.Enabled && language is not null)
        {
            var posts = await _blogService.GetAllBlogPostsAsync(store.Id, store.DefaultLanguageId);
            foreach (var post in posts)
            {
                var slug = await _blogLocalizationService.GetSlugAsync(post, languageId);
                var title = await _blogLocalizationService.GetFieldAsync(post, nameof(BlogPost.Title), languageId, post.Title);
                var body = await _blogLocalizationService.GetFieldAsync(post, nameof(BlogPost.Body), languageId, post.Body);
                var url = BuildAbsoluteLocalizedUrl(language.UniqueSeoCode, slug);
                feed.Items.Add(new RssItem(title, body, new Uri(url),
                    $"urn:store:{store.Id}:blog:post:{post.Id}", post.CreatedOnUtc));
            }
        }

        return new RssActionResult(feed, _webHelper.GetThisPageUrl(includeQueryString: false));
    }

    [HttpGet]
    public async Task<IActionResult> RedirectLegacyContactUs(string language)
    {
        var store = await _storeContext.GetCurrentStoreAsync();
        var languages = await _languageService.GetAllLanguagesAsync(storeId: store.Id);
        var requestedLanguage = languages.FirstOrDefault(item => item.Published &&
            item.UniqueSeoCode.Equals(language, StringComparison.OrdinalIgnoreCase))
            ?? languages.FirstOrDefault(item => item.Id == store.DefaultLanguageId)
            ?? languages.FirstOrDefault();

        var topic = await _topicService.GetTopicBySystemNameAsync("ContactUs", store.Id);
        if (topic is not null && requestedLanguage is not null)
        {
            var slug = await _urlRecordService.GetSeNameAsync(topic.Id, "Topic", requestedLanguage.Id,
                returnDefaultValue: true, ensureTwoPublishedLanguages: false);
            if (!string.IsNullOrWhiteSpace(slug) &&
                !slug.Equals("contactus", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectPermanent(BuildLocalizedPath(requestedLanguage.UniqueSeoCode, slug));
            }
        }

        // Fail safely through the current core contact view when a language
        // package has not supplied a localized contact alias.
        var model = await _commonModelFactory.PrepareContactUsModelAsync(new ContactUsModel(), false);
        return View("~/Views/Common/ContactUs.cshtml", model);
    }

    private string BuildAbsoluteLocalizedUrl(string languageCode, string slug)
    {
        return $"{_webHelper.GetStoreLocation().TrimEnd('/')}{BuildLocalizedPath(languageCode, slug)}";
    }

    private static string BuildLocalizedPath(string languageCode, string slug)
    {
        return $"/{languageCode}/{slug}";
    }
}
