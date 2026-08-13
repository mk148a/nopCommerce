using Microsoft.AspNetCore.Http;
using Nop.Core;
using Nop.Core.Domain.Localization;
using Nop.Services.Localization;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Services;

public interface IBlogRouteLanguageResolver
{
    Task<Language> ResolveAsync();
}

/// <summary>
/// Resolves the language requested by the public URL. This deliberately gives
/// the route/path precedence over a stale working-language cookie so a clean
/// request to /de/blog cannot be projected as an English blog page.
/// </summary>
public sealed class BlogRouteLanguageResolver : IBlogRouteLanguageResolver
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILanguageService _languageService;
    private readonly IStoreContext _storeContext;
    private readonly IWorkContext _workContext;

    public BlogRouteLanguageResolver(IHttpContextAccessor httpContextAccessor,
        ILanguageService languageService,
        IStoreContext storeContext,
        IWorkContext workContext)
    {
        _httpContextAccessor = httpContextAccessor;
        _languageService = languageService;
        _storeContext = storeContext;
        _workContext = workContext;
    }

    public async Task<Language> ResolveAsync()
    {
        var fallback = await _workContext.GetWorkingLanguageAsync();
        var requestedCodes = GetRequestedSeoCodes(_httpContextAccessor.HttpContext);
        if (requestedCodes.Count == 0)
            return fallback;

        var store = await _storeContext.GetCurrentStoreAsync();
        var languages = await _languageService.GetAllLanguagesAsync(storeId: store.Id);
        foreach (var code in requestedCodes)
        {
            var requested = languages.FirstOrDefault(language => language.Published &&
                language.UniqueSeoCode.Equals(code, StringComparison.OrdinalIgnoreCase));
            if (requested is not null)
                return requested;
        }

        return fallback;
    }

    internal static IReadOnlyList<string> GetRequestedSeoCodes(HttpContext context)
    {
        var result = new List<string>(2);
        var pathLanguage = context?.Request.Path.Value?
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(pathLanguage))
            result.Add(pathLanguage);

        if (context?.Request.RouteValues.TryGetValue(NopRoutingDefaults.RouteValue.Language,
                out var routeLanguage) == true && !string.IsNullOrWhiteSpace(routeLanguage?.ToString()))
        {
            if (!result.Contains(routeLanguage.ToString(), StringComparer.OrdinalIgnoreCase))
                result.Add(routeLanguage.ToString());
        }

        return result;
    }
}
