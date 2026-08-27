namespace Nop.Plugin.Misc.HoodLocalizationSeo.Infrastructure;

/// <summary>
/// Uses the configured store URL as the trust anchor and the active store
/// location as the public origin. This keeps generated sitemap URLs aligned
/// with core output when a staging binding uses a different port.
/// </summary>
internal static class SitemapCanonicalOrigin
{
    public static bool TryCreate(string configuredStoreUrl, string activeStoreLocation, out Uri canonicalOrigin)
    {
        canonicalOrigin = null;
        if (!TryParsePublicUri(configuredStoreUrl, out var configured) ||
            !TryParsePublicUri(activeStoreLocation, out var active) ||
            !configured.Scheme.Equals(active.Scheme, StringComparison.OrdinalIgnoreCase) ||
            !configured.IdnHost.Equals(active.IdnHost, StringComparison.OrdinalIgnoreCase) ||
            !IsAllowedActivePort(configured, active))
        {
            return false;
        }

        var activeOrigin = active.GetComponents(UriComponents.SchemeAndServer, UriFormat.UriEscaped);
        var configuredPathBase = configured.AbsolutePath.TrimEnd('/');
        return Uri.TryCreate($"{activeOrigin.TrimEnd('/')}{configuredPathBase}/", UriKind.Absolute,
            out canonicalOrigin);
    }

    private static bool IsAllowedActivePort(Uri configured, Uri active) =>
        active.Port == configured.Port || active.IsDefaultPort;

    private static bool TryParsePublicUri(string value, out Uri uri)
    {
        uri = null;
        return Uri.TryCreate(value, UriKind.Absolute, out uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
               !string.IsNullOrWhiteSpace(uri.Host) &&
               string.IsNullOrEmpty(uri.UserInfo) &&
               string.IsNullOrEmpty(uri.Query) &&
               string.IsNullOrEmpty(uri.Fragment);
    }
}
