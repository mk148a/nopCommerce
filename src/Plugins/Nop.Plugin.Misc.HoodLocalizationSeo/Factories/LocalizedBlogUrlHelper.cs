using System.Text.RegularExpressions;
using Nop.Core;
using Nop.Core.Domain.Blogs;
using Nop.Core.Domain.Localization;
using Nop.Core.Domain.Seo;
using Nop.Plugin.Misc.HoodLocalizationSeo.Services;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Factories;

/// <summary>
/// Keeps generated BlogPost links on the language selected by the current
/// public route. nopCommerce's named generic route has an "en" default which
/// can leak into outbound links when ambient route values are unavailable.
/// </summary>
public sealed class LocalizedBlogUrlHelper : INopUrlHelper
{
    private static readonly Regex LanguagePrefixPattern = new(
        @"^(?<origin>(?:https?://[^/]+)?)/(?<language>[a-z]{2})(?=/|$)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly INopUrlHelper _inner;
    private readonly IBlogRouteLanguageResolver _routeLanguageResolver;
    private readonly LocalizationSettings _localizationSettings;

    public LocalizedBlogUrlHelper(INopUrlHelper inner,
        IBlogRouteLanguageResolver routeLanguageResolver,
        LocalizationSettings localizationSettings)
    {
        _inner = inner;
        _routeLanguageResolver = routeLanguageResolver;
        _localizationSettings = localizationSettings;
    }

    public async Task<string> RouteGenericUrlAsync<TEntity>(object values = null, string protocol = null,
        string host = null, string fragment = null)
        where TEntity : BaseEntity, ISlugSupported
    {
        var url = await _inner.RouteGenericUrlAsync<TEntity>(values, protocol, host, fragment);
        if (typeof(TEntity) != typeof(BlogPost) || !_localizationSettings.SeoFriendlyUrlsForLanguagesEnabled)
            return url;

        var language = await _routeLanguageResolver.ResolveAsync();
        return RewriteLanguagePrefix(url, language?.UniqueSeoCode);
    }

    public Task<string> RouteTopicUrlAsync(string systemName, string protocol = null, string host = null,
        string fragment = null)
    {
        return _inner.RouteTopicUrlAsync(systemName, protocol, host, fragment);
    }

    internal static string RewriteLanguagePrefix(string url, string languageCode)
    {
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(languageCode))
            return url;

        return LanguagePrefixPattern.Replace(url,
            match => $"{match.Groups["origin"].Value}/{languageCode.ToLowerInvariant()}", 1);
    }
}
