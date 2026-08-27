using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Nop.Core.Domain.Stores;
using Nop.Services.Stores;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Infrastructure;

/// <summary>
/// Restricts the seasonal landing's host canonicalization to the configured
/// store origin and its explicit www alias. The redirect target is built from
/// the trusted canonical origin; the request Host is never reflected.
/// </summary>
internal static class HalloweenLandingCanonicalRedirect
{
    private static readonly HashSet<string> TrackingQueryKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "_ga", "_gl", "dclid", "fbclid", "gbraid", "gclid", "mc_cid", "mc_eid", "msclkid", "wbraid"
    };

    internal static bool TryGetCanonicalStoreForHost(IStoreService storeService,
        IEnumerable<Store> stores, HostString requestHost, out Uri canonicalOrigin,
        out CanonicalHostKind hostKind)
    {
        ArgumentNullException.ThrowIfNull(storeService);
        ArgumentNullException.ThrowIfNull(stores);
        canonicalOrigin = null;
        hostKind = CanonicalHostKind.Invalid;
        if (string.IsNullOrWhiteSpace(requestHost.Host))
            return false;

        // This deliberately uses the core host-membership contract rather
        // than interpreting Store.Hosts here. Exactly one matching store is
        // required, so an alias cannot select or redirect between stores.
        var matches = stores.Where(store => storeService.ContainsHostValue(store, requestHost.Host))
            .ToList();
        if (matches.Count != 1)
            return false;

        if (!SitemapCanonicalOrigin.TryCreate(matches[0].Url, matches[0].Url, out canonicalOrigin))
            return false;

        if (requestHost.Host.Equals(canonicalOrigin.IdnHost, StringComparison.OrdinalIgnoreCase))
            hostKind = CanonicalHostKind.Canonical;
        else if (requestHost.Host.Equals($"www.{canonicalOrigin.IdnHost}",
                     StringComparison.OrdinalIgnoreCase))
            hostKind = CanonicalHostKind.WwwAlias;
        else
            hostKind = CanonicalHostKind.OtherRegisteredAlias;

        // The request port is deliberately not part of the host comparison.
        // It is never reflected; Location is built only from this Store.Url.
        return true;
    }

    internal static bool IsLandingGetOrHead(PathString path, string method, out string language)
    {
        language = null;
        if (!HttpMethods.IsGet(method) && !HttpMethods.IsHead(method))
            return false;

        foreach (var candidate in new[] { "en", "tr", "de", "fr", "es" })
        {
            if (!string.Equals(path.Value, HalloweenLandingRoute.BuildPath(candidate),
                    StringComparison.OrdinalIgnoreCase))
                continue;

            language = candidate;
            return true;
        }

        return false;
    }

    internal static string BuildCanonicalUrl(Uri canonicalOrigin, string language) =>
        $"{canonicalOrigin.GetLeftPart(UriPartial.Path).TrimEnd('/')}{HalloweenLandingRoute.BuildPath(language)}";

    internal static string AppendAllowedTrackingQuery(string canonicalUrl, IQueryCollection query)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalUrl);
        ArgumentNullException.ThrowIfNull(query);

        var trackingValues = query
            .Where(pair => IsAllowedTrackingKey(pair.Key))
            .Select(pair => new KeyValuePair<string, StringValues>(pair.Key, pair.Value));
        return canonicalUrl + QueryString.Create(trackingValues);
    }

    private static bool IsAllowedTrackingKey(string key) =>
        key.StartsWith("utm_", StringComparison.OrdinalIgnoreCase) || TrackingQueryKeys.Contains(key);

}

internal enum CanonicalHostKind
{
    Invalid = 0,
    Canonical = 1,
    WwwAlias = 2,
    OtherRegisteredAlias = 3
}
