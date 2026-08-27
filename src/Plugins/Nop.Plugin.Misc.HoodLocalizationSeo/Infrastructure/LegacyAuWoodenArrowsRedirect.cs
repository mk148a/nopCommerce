using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Infrastructure;

/// <summary>
/// Preserves the public destination of the retired Australian Wooden Arrows
/// category used by historical Google Ads. The scope is deliberately one
/// exact GET/HEAD path; product routing, URL records and all other locales
/// continue through nopCommerce unchanged.
/// </summary>
internal static class LegacyAuWoodenArrowsRedirect
{
    internal const string LegacyPath = "/au/wooden-arrows";
    internal const string TargetPath = "/au/wooden-arrows-2";

    // Do not carry catalog filters, return targets, coupons, authentication
    // values, or arbitrary parameters into a permanent SEO redirect. These
    // parameters retain paid-acquisition attribution without changing the
    // target resource or redirecting user-controlled application state.
    private static readonly HashSet<string> SafeAttributionQueryKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "dclid",
        "fbclid",
        "gbraid",
        "gclid",
        "msclkid",
        "utm_campaign",
        "utm_content",
        "utm_id",
        "utm_medium",
        "utm_source",
        "utm_term",
        "wbraid"
    };

    internal static bool TryBuildLocation(HttpRequest request, out string location)
    {
        ArgumentNullException.ThrowIfNull(request);

        location = string.Empty;
        if ((!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method)) ||
            !IsLegacyPath(request.Path))
        {
            return false;
        }

        var target = $"{request.PathBase.ToUriComponent().TrimEnd('/')}{TargetPath}";
        var attribution = request.Query
            .Where(pair => SafeAttributionQueryKeys.Contains(pair.Key))
            .Select(pair => new KeyValuePair<string, StringValues>(pair.Key, pair.Value));

        location = QueryHelpers.AddQueryString(target, attribution);
        return true;
    }

    private static bool IsLegacyPath(PathString path)
    {
        var value = path.Value;
        return LegacyPath.Equals(value, StringComparison.Ordinal) ||
               $"{LegacyPath}/".Equals(value, StringComparison.Ordinal);
    }
}
