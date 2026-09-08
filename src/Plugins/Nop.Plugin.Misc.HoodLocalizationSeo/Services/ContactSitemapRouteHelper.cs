namespace Nop.Plugin.Misc.HoodLocalizationSeo.Services;

internal static class ContactSitemapRouteHelper
{
    public static bool TryGetSlug(string url, string storeLocation, ISet<string> languageCodes, out string slug)
    {
        slug = string.Empty;
        var value = (url ?? string.Empty).Split('?', '#')[0].Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(value))
            return false;
        var isProtocolRelative = value.StartsWith("//", StringComparison.Ordinal);
        var candidate = isProtocolRelative ? $"https:{value}" : value;
        var absolute = Uri.TryCreate(candidate, UriKind.Absolute, out var absoluteUri);
        var storeUri = Uri.TryCreate(storeLocation, UriKind.Absolute, out var parsedStore);

        // Origin checks are intentionally left to the caller. ContactUs
        // localized slugs on foreign/protocol-relative URLs still need entity
        // resolution so they can be removed safely from generated sitemaps.
        var path = (absolute ? absoluteUri.AbsolutePath : value).Trim('/');
        var basePath = storeUri ? parsedStore.AbsolutePath.Trim('/') : string.Empty;
        if (!string.IsNullOrEmpty(basePath))
        {
            if (!path.StartsWith(basePath + "/", StringComparison.OrdinalIgnoreCase))
                return false;
            path = path[(basePath.Length + 1)..];
        }

        try
        {
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 2 && !languageCodes.Contains(Uri.UnescapeDataString(segments[0])))
                return false;
            if (segments.Length is < 1 or > 2)
                return false;

            slug = Uri.UnescapeDataString(segments[^1]);
            return !string.IsNullOrWhiteSpace(slug);
        }
        catch (UriFormatException)
        {
            return false;
        }
    }

    public static bool IsLegacyContactRoute(string url, string storeLocation, ISet<string> languageCodes)
    {
        if (!TryGetSlug(url, storeLocation, languageCodes, out var slug) ||
            !slug.Equals("contactus", StringComparison.OrdinalIgnoreCase))
            return false;
        var value = (url ?? string.Empty).Split('?', '#')[0].Trim().TrimEnd('/');
        var absolute = Uri.TryCreate(value, UriKind.Absolute, out var uri);
        var path = (absolute ? uri.AbsolutePath : value).Trim('/');
        var basePath = Uri.TryCreate(storeLocation, UriKind.Absolute, out var store)
            ? store.AbsolutePath.Trim('/') : string.Empty;
        if (!string.IsNullOrEmpty(basePath) && path.StartsWith(basePath + "/", StringComparison.OrdinalIgnoreCase))
            path = path[(basePath.Length + 1)..];
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length == 2 && languageCodes.Contains(segments[0]);
    }
}
