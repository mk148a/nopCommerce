using Microsoft.AspNetCore.Http;
using Nop.Core;
using Nop.Core.Domain.Topics;
using Nop.Core.Domain.Catalog;
using Nop.Core.Events;
using Nop.Core.Http;
using Nop.Services.Events;
using Nop.Services.Localization;
using Nop.Services.Catalog;
using Nop.Services.Seo;
using Nop.Services.Topics;
using Nop.Web.Framework.Events;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Consumers;

public sealed class RoutingEventConsumer : IConsumer<GenericRoutingEvent>
{
    private readonly ILanguageService _languageService;
    private readonly IStoreContext _storeContext;
    private readonly IProductService _productService;
    private readonly ITopicService _topicService;
    private readonly IUrlRecordService _urlRecordService;

    public RoutingEventConsumer(ILanguageService languageService,
        IStoreContext storeContext,
        IProductService productService,
        ITopicService topicService,
        IUrlRecordService urlRecordService)
    {
        _languageService = languageService;
        _storeContext = storeContext;
        _productService = productService;
        _topicService = topicService;
        _urlRecordService = urlRecordService;
    }

    public async Task HandleEventAsync(GenericRoutingEvent eventMessage)
    {
        ArgumentNullException.ThrowIfNull(eventMessage);

        var language = await GetRequestedLanguageAsync(eventMessage);
        if (language is null)
            return;

        var urlRecord = eventMessage.UrlRecord;
        eventMessage.RouteValues.TryGetValue(NopRoutingDefaults.RouteValue.Language, out var languageValue);
        var routeLanguageCode = languageValue?.ToString();
        var requestPath = eventMessage.HttpContext.Request.Path.Value ?? string.Empty;
        var firstPathSegment = requestPath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
        var hasLanguageCode = routeLanguageCode?.Length == 2 &&
            string.Equals(firstPathSegment, routeLanguageCode, StringComparison.OrdinalIgnoreCase);

        if (urlRecord.EntityName.Equals(nameof(Product), StringComparison.OrdinalIgnoreCase))
        {
            var product = await _productService.GetProductByIdAsync(urlRecord.EntityId);
            if (product is null || product.Deleted)
            {
                eventMessage.HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
                eventMessage.RouteValues[NopRoutingDefaults.RouteValue.Controller] = "Common";
                eventMessage.RouteValues[NopRoutingDefaults.RouteValue.Action] = "PageNotFound";
                StopRouting(eventMessage);
                return;
            }
        }

        // Language-neutral inactive records reached without a language prefix
        // must canonicalize directly to the store default language slug.
        if ((!hasLanguageCode || string.Equals(languageValue?.ToString(), language.UniqueSeoCode,
                StringComparison.OrdinalIgnoreCase)) &&
            urlRecord.LanguageId == 0 && !urlRecord.IsActive)
        {
            var store = await _storeContext.GetCurrentStoreAsync();
            if (language.Id == store.DefaultLanguageId)
            {
                var activeDefaultSlug = await _urlRecordService.GetActiveSlugAsync(
                    urlRecord.EntityId, urlRecord.EntityName, 0);
                if (!string.IsNullOrWhiteSpace(activeDefaultSlug))
                {
                    SetPermanentRedirect(eventMessage, $"/{language.UniqueSeoCode}/{activeDefaultSlug}");
                    return;
                }
            }
        }

        var activeLocalizedSlug = await _urlRecordService.GetActiveSlugAsync(
            urlRecord.EntityId, urlRecord.EntityName, language.Id);

        // Redirect retired slugs and a valid slug from the wrong language to
        // the active slug for the language code in the current request.
        if (!string.IsNullOrWhiteSpace(activeLocalizedSlug) &&
            (!urlRecord.IsActive ||
             !activeLocalizedSlug.Equals(urlRecord.Slug, StringComparison.OrdinalIgnoreCase)))
        {
            SetPermanentRedirect(eventMessage, $"/{language.UniqueSeoCode}/{activeLocalizedSlug}");
            return;
        }

        if (!urlRecord.EntityName.Equals(nameof(Topic), StringComparison.OrdinalIgnoreCase))
            return;

        var topic = await _topicService.GetTopicByIdAsync(urlRecord.EntityId);
        if (topic?.SystemName.Equals("ContactUs", StringComparison.OrdinalIgnoreCase) != true)
            return;

        // Render the contact form at the localized Topic URL. This deliberately
        // avoids TopicController and keeps the public URL/canonical unchanged.
        eventMessage.RouteValues[NopRoutingDefaults.RouteValue.Controller] = "Common";
        eventMessage.RouteValues[NopRoutingDefaults.RouteValue.Action] = "ContactUs";
        eventMessage.RouteValues[NopRoutingDefaults.RouteValue.SeName] = urlRecord.Slug;
        StopRouting(eventMessage);
    }

    private async Task<Nop.Core.Domain.Localization.Language> GetRequestedLanguageAsync(GenericRoutingEvent eventMessage)
    {
        var store = await _storeContext.GetCurrentStoreAsync();
        var languages = await _languageService.GetAllLanguagesAsync(storeId: store.Id);
        if (eventMessage.RouteValues.TryGetValue(NopRoutingDefaults.RouteValue.Language, out var value))
        {
            var language = languages.FirstOrDefault(item => item.Published &&
                item.UniqueSeoCode.Equals(value?.ToString(), StringComparison.OrdinalIgnoreCase));
            if (language is not null)
                return language;
        }

        return languages.FirstOrDefault(item => item.Id == store.DefaultLanguageId)
            ?? languages.FirstOrDefault(item => item.Published);
    }

    private static void SetPermanentRedirect(GenericRoutingEvent eventMessage, string path)
    {
        eventMessage.RouteValues[NopRoutingDefaults.RouteValue.Controller] = "Common";
        eventMessage.RouteValues[NopRoutingDefaults.RouteValue.Action] = "InternalRedirect";
        eventMessage.RouteValues[NopRoutingDefaults.RouteValue.Url] =
            $"{eventMessage.HttpContext.Request.PathBase}{path}{eventMessage.HttpContext.Request.QueryString}";
        eventMessage.RouteValues[NopRoutingDefaults.RouteValue.PermanentRedirect] = true;
        eventMessage.HttpContext.Items[NopHttpDefaults.GenericRouteInternalRedirect] = true;
        StopRouting(eventMessage);
    }

    private static void StopRouting(GenericRoutingEvent eventMessage)
    {
        // nopCommerce 4.80 calls this flag Handled; 4.90+ renamed it to
        // StopProcessing. Reflection keeps this source compatible with both
        // contracts while each major-version package is rebuilt normally.
        var property = eventMessage.GetType().GetProperty("StopProcessing")
            ?? eventMessage.GetType().GetProperty("Handled")
            ?? throw new InvalidOperationException("GenericRoutingEvent has no stop-processing flag.");
        property.SetValue(eventMessage, true);
    }
}
