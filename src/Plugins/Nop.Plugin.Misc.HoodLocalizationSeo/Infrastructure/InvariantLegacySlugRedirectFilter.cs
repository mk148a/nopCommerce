using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Nop.Core;
using Nop.Services.Localization;
using Nop.Services.Seo;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Infrastructure;

/// <summary>
/// Redirects an inactive, language-neutral legacy slug to the active slug under
/// the store's default language prefix.
/// </summary>
public sealed class InvariantLegacySlugRedirectFilter : IAsyncActionFilter
{
    private readonly ILanguageService _languageService;
    private readonly IStoreContext _storeContext;
    private readonly IUrlRecordService _urlRecordService;

    public InvariantLegacySlugRedirectFilter(ILanguageService languageService,
        IStoreContext storeContext,
        IUrlRecordService urlRecordService)
    {
        _languageService = languageService;
        _storeContext = storeContext;
        _urlRecordService = urlRecordService;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method))
        {
            await next();
            return;
        }

        var segments = request.Path.Value?.Trim('/').Split('/',
            StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
        if (segments.Length != 1)
        {
            await next();
            return;
        }

        var firstSegment = segments[0];

        var store = await _storeContext.GetCurrentStoreAsync();
        var languages = await _languageService.GetAllLanguagesAsync(storeId: store.Id);
        if (languages.Any(language => language.Published &&
            string.Equals(language.UniqueSeoCode, firstSegment, StringComparison.OrdinalIgnoreCase)))
        {
            await next();
            return;
        }

        var urlRecord = await _urlRecordService.GetBySlugAsync(firstSegment);
        if (urlRecord is null || urlRecord.LanguageId != 0 || urlRecord.IsActive)
        {
            await next();
            return;
        }

        var defaultLanguage = languages.FirstOrDefault(language => language.Id == store.DefaultLanguageId);
        if (defaultLanguage is null || string.IsNullOrWhiteSpace(defaultLanguage.UniqueSeoCode))
        {
            await next();
            return;
        }

        var activeSlug = await _urlRecordService.GetActiveSlugAsync(
            urlRecord.EntityId, urlRecord.EntityName, languageId: 0);
        if (string.IsNullOrWhiteSpace(activeSlug))
        {
            await next();
            return;
        }

        context.Result = new RedirectResult(
            $"{request.PathBase}/{defaultLanguage.UniqueSeoCode}/{activeSlug}{request.QueryString}",
            permanent: true);
    }
}
