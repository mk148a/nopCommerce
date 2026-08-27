using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nop.Core;
using Nop.Core.Domain.Seo;
using Nop.Plugin.Misc.HoodLocalizationSeo.Infrastructure;
using Nop.Plugin.Misc.HoodLocalizationSeo.Services;
using Nop.Services.Events;
using Nop.Services.Localization;
using Nop.Services.Topics;
using Nop.Web.Framework.Events;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Consumers;

public sealed class PageRenderingEventConsumer : IConsumer<PageRenderingEvent>
{
    private static readonly HashSet<string> CanonicalRoutes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Homepage", "Blog", "BlogPost", "Sitemap"
    };

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IBlogTagHreflangService _blogTagHreflangService;
    private readonly ILocalizationService _localizationService;
    private readonly SeoSettings _seoSettings;
    private readonly IStoreContext _storeContext;
    private readonly ITopicService _topicService;
    private readonly IWorkContext _workContext;

    public PageRenderingEventConsumer(IHttpContextAccessor httpContextAccessor,
        IBlogTagHreflangService blogTagHreflangService,
        ILocalizationService localizationService,
        SeoSettings seoSettings,
        IStoreContext storeContext,
        ITopicService topicService,
        IWorkContext workContext)
    {
        _httpContextAccessor = httpContextAccessor;
        _blogTagHreflangService = blogTagHreflangService;
        _localizationService = localizationService;
        _seoSettings = seoSettings;
        _storeContext = storeContext;
        _topicService = topicService;
        _workContext = workContext;
    }

    public async Task HandleEventAsync(PageRenderingEvent eventMessage)
    {
        var context = _httpContextAccessor.HttpContext;
        if (context is null || context.GetRouteValue("area") is not null)
            return;

        var routeName = eventMessage.GetRouteName(handleDefaultRoutes: true) ?? string.Empty;
        var controller = context.GetRouteValue(NopRoutingDefaults.RouteValue.Controller)?.ToString();
        var action = context.GetRouteValue(NopRoutingDefaults.RouteValue.Action)?.ToString();
        var isBlogByTag = routeName.Equals("BlogByTag", StringComparison.OrdinalIgnoreCase) ||
                          controller?.Equals("Blog", StringComparison.OrdinalIgnoreCase) == true &&
                          action?.Equals("BlogByTag", StringComparison.OrdinalIgnoreCase) == true;
        var isContactUs = IsContactUsAction(controller, action);
        var isProductsByTag = controller?.Equals("Catalog", StringComparison.OrdinalIgnoreCase) == true &&
                              action?.Equals("ProductsByTag", StringComparison.OrdinalIgnoreCase) == true;
        var isHalloweenLanding = routeName.Equals("HoodHalloweenLanding", StringComparison.OrdinalIgnoreCase) ||
                                  controller?.Equals("HalloweenLanding", StringComparison.OrdinalIgnoreCase) == true &&
                                  action?.Equals("HalloweenLanding", StringComparison.OrdinalIgnoreCase) == true;

        if (_seoSettings.CanonicalUrlsEnabled &&
            (CanonicalRoutes.Contains(routeName) || isHalloweenLanding || IsCanonicalPublicAction(controller, action)))
        {
            var request = context.Request;
            var canonical = $"{request.Scheme}://{request.Host}{request.PathBase}{request.Path}".ToLowerInvariant();
            eventMessage.Helper.AddCanonicalUrlParts(canonical,
                _seoSettings.QueryStringInCanonicalUrlsEnabled);
        }

        if (isContactUs)
            await AddLocalizedContactMetadataAsync(eventMessage);

        if (isBlogByTag || isProductsByTag)
            eventMessage.Helper.AddHeadCustomParts("<meta name=\"robots\" content=\"noindex,follow\" />");

        if (isBlogByTag)
        {
            await AddLocalizedBlogTagHreflangAsync(eventMessage, context);
        }
    }

    private async Task AddLocalizedContactMetadataAsync(PageRenderingEvent eventMessage)
    {
        var store = await _storeContext.GetCurrentStoreAsync();
        var topic = await _topicService.GetTopicBySystemNameAsync("ContactUs", store.Id);
        if (topic is null)
            return;

        var metaDescription = await _localizationService.GetLocalizedAsync(topic,
            entity => entity.MetaDescription);
        var metaKeywords = await _localizationService.GetLocalizedAsync(topic,
            entity => entity.MetaKeywords);
        if (!string.IsNullOrWhiteSpace(metaDescription))
            eventMessage.Helper.AddMetaDescriptionParts(metaDescription);
        if (!string.IsNullOrWhiteSpace(metaKeywords))
            eventMessage.Helper.AddMetaKeywordParts(metaKeywords);
    }

    private async Task AddLocalizedBlogTagHreflangAsync(PageRenderingEvent eventMessage, HttpContext context)
    {
        var requestedTag = context.GetRouteValue("tag")?.ToString();
        if (string.IsNullOrWhiteSpace(requestedTag))
            return;

        var language = await _workContext.GetWorkingLanguageAsync();
        var store = await _storeContext.GetCurrentStoreAsync();
        var targets = await _blogTagHreflangService.GetTargetsAsync(requestedTag, language, store);
        var defaultTarget = targets.SingleOrDefault(target => target.IsDefault);
        if (defaultTarget is null)
            return;

        AddHreflang(eventMessage, context.Request, "x-default", defaultTarget);
        foreach (var target in targets)
            AddHreflang(eventMessage, context.Request, target.LanguageCulture, target);
    }

    private static void AddHreflang(PageRenderingEvent eventMessage,
        HttpRequest request,
        string hreflang,
        BlogTagHreflangTarget target)
    {
        var pathBase = request.PathBase.HasValue ? request.PathBase.Value.TrimEnd('/') : string.Empty;
        var path = $"{pathBase}/{Uri.EscapeDataString(target.LanguageCode)}/blog/tag/{Uri.EscapeDataString(target.Tag)}";
        var href = $"{request.Scheme}://{request.Host}{path}{request.QueryString}";
        AddHreflang(eventMessage, hreflang, href);
    }

    private static void AddHreflang(PageRenderingEvent eventMessage,
        string hreflang,
        string href)
    {
        eventMessage.Helper.AddHeadCustomParts(
            $"<link rel=\"alternate\" hreflang=\"{HtmlEncoder.Default.Encode(hreflang)}\" href=\"{HtmlEncoder.Default.Encode(href)}\" />");
    }

    private static bool IsCanonicalPublicAction(string controller, string action)
    {
        return controller?.Equals("Home", StringComparison.OrdinalIgnoreCase) == true ||
               controller?.Equals("Blog", StringComparison.OrdinalIgnoreCase) == true ||
               controller?.Equals("Common", StringComparison.OrdinalIgnoreCase) == true &&
               (action?.Equals("Sitemap", StringComparison.OrdinalIgnoreCase) == true ||
                action?.Equals("ContactUs", StringComparison.OrdinalIgnoreCase) == true);
    }

    private static bool IsContactUsAction(string controller, string action) =>
        controller?.Equals("Common", StringComparison.OrdinalIgnoreCase) == true &&
        action?.Equals("ContactUs", StringComparison.OrdinalIgnoreCase) == true;

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

        return Uri.TryCreate(storeUri.GetLeftPart(UriPartial.Path).TrimEnd('/') + "/",
            UriKind.Absolute, out canonicalOrigin);
    }
}
