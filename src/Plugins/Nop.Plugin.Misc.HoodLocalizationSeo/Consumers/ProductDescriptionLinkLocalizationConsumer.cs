using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Localization;
using Nop.Services.Events;
using Nop.Services.Seo;
using Nop.Web.Framework.Events;
using Nop.Web.Framework.Models;
using Nop.Web.Models.Catalog;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Consumers;

/// <summary>
/// Rewrites absolute product links embedded in the public product description
/// to the active language route. Links that cannot be resolved as products are
/// intentionally left untouched.
/// </summary>
public sealed class ProductDescriptionLinkLocalizationConsumer : IConsumer<ModelPreparedEvent<BaseNopModel>>
{
    private static readonly Regex AbsoluteHrefRegex = new(
        "(?<prefix>\\bhref\\s*=\\s*)(?<quote>[\\\"'])(?<url>https?://[^\\\"']+)(?:\\k<quote>)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly LocalizationSettings _localizationSettings;
    private readonly IStoreContext _storeContext;
    private readonly IUrlRecordService _urlRecordService;
    private readonly IWorkContext _workContext;

    public ProductDescriptionLinkLocalizationConsumer(IHttpContextAccessor httpContextAccessor,
        LocalizationSettings localizationSettings,
        IStoreContext storeContext,
        IUrlRecordService urlRecordService,
        IWorkContext workContext)
    {
        _httpContextAccessor = httpContextAccessor;
        _localizationSettings = localizationSettings;
        _storeContext = storeContext;
        _urlRecordService = urlRecordService;
        _workContext = workContext;
    }

    public async Task HandleEventAsync(ModelPreparedEvent<BaseNopModel> eventMessage)
    {
        var context = _httpContextAccessor.HttpContext;
        if (context is null ||
            context.Request.RouteValues.TryGetValue("area", out var area) && area is not null ||
            eventMessage?.Model is not ProductDetailsModel model ||
            string.IsNullOrWhiteSpace(model.FullDescription))
            return;

        var language = await _workContext.GetWorkingLanguageAsync();
        if (language is null || language.Id <= 0 || string.IsNullOrWhiteSpace(language.UniqueSeoCode))
            return;

        var store = await _storeContext.GetCurrentStoreAsync();
        if (store is null || !Uri.TryCreate(store.Url, UriKind.Absolute, out var storeOrigin))
            return;

        model.FullDescription = await RewriteProductLinksAsync(model.FullDescription, language.Id,
            language.UniqueSeoCode, storeOrigin, _localizationSettings.SeoFriendlyUrlsForLanguagesEnabled);
    }

    private async Task<string> RewriteProductLinksAsync(string html, int languageId, string languageCode,
        Uri storeOrigin, bool includeLanguagePrefix)
    {
        var matches = AbsoluteHrefRegex.Matches(html);
        if (matches.Count == 0)
            return html;

        var replacements = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match match in matches)
        {
            var urlText = match.Groups["url"].Value;
            if (!Uri.TryCreate(urlText, UriKind.Absolute, out var uri) ||
                !uri.Scheme.Equals(storeOrigin.Scheme, StringComparison.OrdinalIgnoreCase) ||
                !uri.Host.Equals(storeOrigin.Host, StringComparison.OrdinalIgnoreCase) ||
                uri.Port != storeOrigin.Port)
                continue;

            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
                continue;

            string slug;
            try
            {
                slug = Uri.UnescapeDataString(segments[^1]);
            }
            catch (UriFormatException)
            {
                continue;
            }

            var record = await _urlRecordService.GetBySlugAsync(slug);
            if (record is null || !record.EntityName.Equals(nameof(Product), StringComparison.OrdinalIgnoreCase))
                continue;

            var localizedSlug = await _urlRecordService.GetSeNameAsync(record.EntityId, nameof(Product), languageId,
                returnDefaultValue: false, ensureTwoPublishedLanguages: false);
            if (string.IsNullOrWhiteSpace(localizedSlug))
                continue;

            var builder = new UriBuilder(uri)
            {
                Path = BuildLocalizedPath(storeOrigin.AbsolutePath, languageCode, localizedSlug,
                    includeLanguagePrefix)
            };
            replacements[urlText] = builder.Uri.AbsoluteUri;
        }

        if (replacements.Count == 0)
            return html;

        return AbsoluteHrefRegex.Replace(html, match =>
        {
            var urlText = match.Groups["url"].Value;
            return replacements.TryGetValue(urlText, out var replacement)
                ? $"{match.Groups["prefix"].Value}{match.Groups["quote"].Value}{replacement}{match.Groups["quote"].Value}"
                : match.Value;
        });
    }

    private static string BuildLocalizedPath(string basePath, string languageCode, string slug,
        bool includeLanguagePrefix)
    {
        var prefix = string.IsNullOrWhiteSpace(basePath) || basePath == "/"
            ? string.Empty
            : "/" + basePath.Trim('/');
        return includeLanguagePrefix
            ? $"{prefix}/{languageCode.Trim('/')}/{slug.Trim('/')}"
            : $"{prefix}/{slug.Trim('/')}";
    }
}
