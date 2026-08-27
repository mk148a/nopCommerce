using Microsoft.AspNetCore.Http;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Infrastructure;

/// <summary>
/// Identifies the legacy ProductContactUsTab endpoint. It is an auxiliary
/// product-tab response, not a standalone product document, so crawlers must
/// not index it as a duplicate product URL.
/// </summary>
internal static class ProductContactUsTabNoIndex
{
    internal const string HeaderName = "X-Robots-Tag";
    internal const string ProductContactUsTabHeaderValue = "noindex, nofollow";
    internal const string UtilityEndpointHeaderValue = "noindex, follow";

    internal static bool IsEligibleRequest(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return TryGetHeaderValue(request, out _);
    }

    /// <summary>
    /// Adds the crawler directive before an endpoint can start the response.
    /// The request, status code and response body are otherwise untouched.
    /// </summary>
    internal static bool TryApply(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Response.HasStarted || !TryGetHeaderValue(context.Request, out var headerValue))
            return false;

        context.Response.Headers[HeaderName] = headerValue;
        return true;
    }

    internal static bool TryGetHeaderValue(HttpRequest request, out string headerValue)
    {
        ArgumentNullException.ThrowIfNull(request);

        headerValue = null;
        if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method))
            return false;

        var path = request.Path.Value ?? string.Empty;
        if (IsProductContactUsTabPath(path))
        {
            headerValue = ProductContactUsTabHeaderValue;
            return true;
        }

        if (IsLocalizedUtilityPath(path, "filterSearch") ||
            IsLocalizedUtilityPath(path, "recentlyviewedproducts") ||
            IsLocalizedUtilityPath(path, "compareproducts"))
        {
            headerValue = UtilityEndpointHeaderValue;
            return true;
        }

        return false;
    }

    private static bool IsProductContactUsTabPath(string path)
    {
        if (string.IsNullOrEmpty(path) || path[0] != '/')
            return false;

        // nopCommerce accepts one optional terminal slash on these GET routes.
        // Do not normalize more broadly: extra path segments must not match.
        if (path.Length > 1 && path[^1] == '/')
            path = path[..^1];

        var segments = path[1..].Split('/', StringSplitOptions.None);
        var offset = 0;
        if (segments.Length == 4 && IsCultureSegment(segments[0]))
            offset = 1;
        else if (segments.Length != 3)
            return false;

        return segments.Length == offset + 3 &&
               segments[offset].Equals("ProductTab", StringComparison.OrdinalIgnoreCase) &&
               segments[offset + 1].Equals("ProductContactUsTab", StringComparison.OrdinalIgnoreCase) &&
               IsAsciiDigits(segments[offset + 2]);
    }

    private static bool IsLocalizedUtilityPath(string path, string endpoint)
    {
        if (string.IsNullOrEmpty(path) || path[0] != '/')
            return false;

        if (path.Length > 1 && path[^1] == '/')
            path = path[..^1];

        var segments = path[1..].Split('/', StringSplitOptions.None);
        return segments.Length == 2 &&
               IsCultureSegment(segments[0]) &&
               segments[1].Equals(endpoint, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCultureSegment(string segment) =>
        segment.Length == 2 && char.IsLetter(segment[0]) && char.IsLetter(segment[1]) ||
        segment.Length == 5 && char.IsLetter(segment[0]) && char.IsLetter(segment[1]) &&
        segment[2] == '-' && char.IsLetter(segment[3]) && char.IsLetter(segment[4]);

    private static bool IsAsciiDigits(string value)
    {
        if (value.Length == 0)
            return false;

        foreach (var character in value)
        {
            if (character is < '0' or > '9')
                return false;
        }

        return true;
    }
}
