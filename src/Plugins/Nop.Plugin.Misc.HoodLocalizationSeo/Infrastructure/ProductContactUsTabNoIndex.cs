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

        if (!TryGetHeaderValue(context.Request, out var headerValue))
            return false;

        if (context.Response.HasStarted)
            return false;

        context.Response.OnStarting(static state =>
        {
            var (httpContext, value) = ((HttpContext, string))state;
            httpContext.Response.Headers[HeaderName] = MergeDirective(
                httpContext.Response.Headers[HeaderName].ToString(), value);
            return Task.CompletedTask;
        }, (context, headerValue));
        context.Response.Headers[HeaderName] = MergeDirective(
            context.Response.Headers[HeaderName].ToString(), headerValue);
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

        if (IsUtilityPath(path, "filterSearch") ||
            IsUtilityPath(path, "recentlyviewedproducts") ||
            IsUtilityPath(path, "compareproducts"))
        {
            headerValue = UtilityEndpointHeaderValue;
            return true;
        }

        if (IsLocalizedAllPath(path, "producttag") || IsLocalizedAllPath(path, "manufacturer") ||
            IsLocalizedQueryPath(path, "newproducts") && request.QueryString.HasValue)
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

    private static bool IsUtilityPath(string path, string endpoint)
    {
        if (string.IsNullOrEmpty(path) || path[0] != '/')
            return false;

        if (path.Length > 1 && path[^1] == '/')
            path = path[..^1];

        var segments = path[1..].Split('/', StringSplitOptions.None);
        return (segments.Length == 1 && segments[0].Equals(endpoint, StringComparison.OrdinalIgnoreCase)) ||
               (segments.Length == 2 && IsCultureSegment(segments[0]) &&
                segments[1].Equals(endpoint, StringComparison.OrdinalIgnoreCase));
    }

    private static string MergeDirective(string existing, string required)
    {
        if (string.IsNullOrWhiteSpace(existing))
            return required;
        var values = existing.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        var requiredTokens = required.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        values.RemoveAll(value => value.Equals("index", StringComparison.OrdinalIgnoreCase) ||
                                  value.Equals("follow", StringComparison.OrdinalIgnoreCase) ||
                                  value.Equals("nofollow", StringComparison.OrdinalIgnoreCase));
        foreach (var token in requiredTokens)
            if (!values.Any(value => value.Equals(token, StringComparison.OrdinalIgnoreCase)))
                values.Add(token);
        return string.Join(", ", values);
    }

    private static bool IsLocalizedAllPath(string path, string endpoint)
    {
        var segments = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var offset = segments.Length > 0 && IsCultureSegment(segments[0]) ? 1 : 0;
        return segments.Length == offset + 2 && (offset == 0 || IsCultureSegment(segments[0])) &&
               segments[offset].Equals(endpoint, StringComparison.OrdinalIgnoreCase) &&
               segments[offset + 1].Equals("all", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLocalizedQueryPath(string path, string endpoint)
    {
        var segments = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var offset = segments.Length > 0 && IsCultureSegment(segments[0]) ? 1 : 0;
        return segments.Length == offset + 1 &&
               segments[offset].Equals(endpoint, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCultureSegment(string segment) =>
        segment.Length == 2 && segment.All(character => character is >= 'A' and <= 'Z' or >= 'a' and <= 'z');

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
