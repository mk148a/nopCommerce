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
        return true;
    }

    internal static bool TryGetHeaderValue(HttpRequest request, out string headerValue)
    {
        ArgumentNullException.ThrowIfNull(request);

        headerValue = null;
        if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method))
            return false;

        if (IsProductContactUsTabRequest(request))
        {
            headerValue = ProductContactUsTabHeaderValue;
            return true;
        }

        if (IsUtilityRequest(request))
        {
            headerValue = UtilityEndpointHeaderValue;
            return true;
        }

        var path = request.Path.Value ?? string.Empty;
        if (IsLocalizedAllPath(path, "producttag") || IsLocalizedAllPath(path, "manufacturer") ||
            IsLocalizedQueryPath(path, "newproducts") && request.QueryString.HasValue)
        {
            headerValue = UtilityEndpointHeaderValue;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Identifies the three public, non-canonical utility endpoints after routing.
    /// Localized requests must have reached their actual controller/action.  The
    /// locale-less variants deliberately accept the GenericUrl redirect endpoint:
    /// nopCommerce sends those historical forms to the current language URL.
    /// </summary>
    internal static bool IsUtilityRequest(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!TryGetUtilityRoute(request.Path.Value ?? string.Empty, out var route, out var isLocaleLess))
            return false;

        return IsExpectedEndpoint(request, route, isLocaleLess);
    }

    private static bool IsProductContactUsTabRequest(HttpRequest request)
    {
        var path = request.Path.Value ?? string.Empty;
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
               IsAsciiDigits(segments[offset + 2]) &&
               IsEndpoint(request, "ProductTab", "ProductContactUsTab");
    }

    private static bool TryGetUtilityRoute(string path, out UtilityRoute route, out bool isLocaleLess)
    {
        route = default;
        isLocaleLess = false;

        if (string.IsNullOrEmpty(path) || path[0] != '/')
            return false;

        if (path.Length > 1 && path[^1] == '/')
            path = path[..^1];

        var segments = path[1..].Split('/', StringSplitOptions.None);
        if (segments.Length == 1)
        {
            isLocaleLess = true;
            return TryParseUtilityEndpoint(segments[0], out route);
        }

        return segments.Length == 2 &&
               IsCultureSegment(segments[0]) &&
               TryParseUtilityEndpoint(segments[1], out route);
    }

    private static bool TryParseUtilityEndpoint(string endpoint, out UtilityRoute route)
    {
        if (endpoint.Equals("filterSearch", StringComparison.OrdinalIgnoreCase))
        {
            route = UtilityRoute.FilterSearch;
            return true;
        }

        if (endpoint.Equals("recentlyviewedproducts", StringComparison.OrdinalIgnoreCase))
        {
            route = UtilityRoute.RecentlyViewedProducts;
            return true;
        }

        if (endpoint.Equals("compareproducts", StringComparison.OrdinalIgnoreCase))
        {
            route = UtilityRoute.CompareProducts;
            return true;
        }

        route = default;
        return false;
    }

    private static bool IsExpectedEndpoint(HttpRequest request, UtilityRoute route, bool isLocaleLess)
    {
        if (isLocaleLess && IsEndpoint(request, "Common", "GenericUrl"))
            return true;

        return route switch
        {
            UtilityRoute.FilterSearch => IsEndpoint(request, "Catalog7Spikes", "AjaxFiltersSearch"),
            UtilityRoute.RecentlyViewedProducts => IsEndpoint(request, "Product", "RecentlyViewedProducts"),
            UtilityRoute.CompareProducts => IsEndpoint(request, "Product", "CompareProducts"),
            _ => false
        };
    }

    private static bool IsEndpoint(HttpRequest request, string controller, string action) =>
        request.RouteValues.TryGetValue("controller", out var controllerValue) &&
        request.RouteValues.TryGetValue("action", out var actionValue) &&
        controllerValue?.ToString()?.Equals(controller, StringComparison.OrdinalIgnoreCase) == true &&
        actionValue?.ToString()?.Equals(action, StringComparison.OrdinalIgnoreCase) == true;

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

    private enum UtilityRoute
    {
        FilterSearch,
        RecentlyViewedProducts,
        CompareProducts
    }
}
