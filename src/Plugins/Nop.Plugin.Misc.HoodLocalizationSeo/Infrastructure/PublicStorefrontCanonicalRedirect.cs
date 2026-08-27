using Microsoft.AspNetCore.Http;
using Nop.Core.Domain.Stores;
using Nop.Services.Stores;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Infrastructure;

/// <summary>
/// Canonicalizes the explicitly registered <c>www</c> alias of a public
/// storefront. The target authority and application path base always come
/// from <see cref="Store.Url"/>; the request Host is never reflected.
/// </summary>
internal static class PublicStorefrontCanonicalRedirect
{
    // These are exact nopCommerce 4.80 and installed-plugin route families
    // that either depend on the current customer/cart cookie, mutate
    // per-customer state, carry a one-time token, deliver a protected file,
    // or complete auth/payment.
    // Redirecting them across host-only cookie boundaries could silently lose
    // state, so canonicalization is intentionally limited to public content.
    private static readonly string[] SensitivePathPrefixes =
    [
        "/.well-known",
        "/addproducttocart",
        "/admin",
        "/amazon-pay",
        "/api",
        "/backinstocksubscribe",
        "/backinstocksubscribesend",
        "/backinstocksubscriptions",
        "/boards/forumsubscriptions",
        "/boards/forumwatch",
        "/boards/topicwatch",
        "/callback",
        "/cart",
        "/changecurrency",
        "/changelanguage",
        "/changetaxtype",
        "/checkout",
        "/clearcomparelist",
        "/compareproducts",
        "/customer",
        "/deletepm",
        "/download",
        "/download-tax-exemption-certificate",
        "/emailwishlist",
        "/externalauthentication",
        "/health",
        "/healthz",
        "/inboxupdate",
        "/live",
        "/liveness",
        "/login",
        "/logout",
        "/metrics",
        "/multi-factor-verification",
        "/newsletter/subscriptionactivation",
        "/omnisend/abandonedcheckout",
        "/onepagecheckout",
        "/order",
        "/orderdetails",
        "/passwordrecovery",
        "/payment",
        "/paypal",
        "/privatemessages",
        "/product/estimateshipping",
        "/productemailafriend",
        "/ready",
        "/readiness",
        "/recentlyviewedproducts",
        "/register",
        "/registerresult",
        "/reorder",
        "/returnrequest",
        "/sendpm",
        "/sentupdate",
        "/setstoretheme",
        "/shoppingcart",
        "/stripe",
        "/subscribenewsletter",
        "/viewpm",
        "/webhook",
        "/wishlist"
    ];

    private static readonly string[] StaticPathPrefixes =
    [
        "/bundles",
        "/content",
        "/css",
        "/images",
        "/js",
        "/lib",
        "/plugins",
        "/themes"
    ];

    private static readonly HashSet<string> StaticExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".avi", ".css", ".eot", ".gif", ".ico", ".jpeg", ".jpg", ".js", ".json", ".map",
        ".mov", ".mp4", ".pdf", ".png", ".svg", ".ttf", ".webm", ".webp", ".woff", ".woff2",
        ".zip"
    };

    internal static bool IsEligibleRequest(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method))
            return false;

        var path = StripOptionalCulturePrefix(request.Path.Value ?? string.Empty);
        if (SensitivePathPrefixes.Any(prefix => HasPathPrefix(path, prefix)) ||
            StaticPathPrefixes.Any(prefix => HasPathPrefix(path, prefix)))
        {
            return false;
        }

        // XML is intentionally not classified as static: sitemap aliases must
        // canonicalize in exactly the same way as public HTML and watch pages.
        var extension = Path.GetExtension(path);
        return string.IsNullOrEmpty(extension) || !StaticExtensions.Contains(extension);
    }

    internal static bool IsPotentialWwwAlias(HostString requestHost) =>
        !string.IsNullOrWhiteSpace(requestHost.Host) &&
        requestHost.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) &&
        requestHost.Host.Length > 4;

    internal static bool TryGetCanonicalStoreForWwwAlias(IStoreService storeService,
        IEnumerable<Store> stores, HostString requestHost, out Uri canonicalBase,
        out CanonicalHostKind hostKind)
    {
        ArgumentNullException.ThrowIfNull(storeService);
        ArgumentNullException.ThrowIfNull(stores);

        canonicalBase = null;
        hostKind = CanonicalHostKind.Invalid;
        if (!IsPotentialWwwAlias(requestHost))
            return false;

        // Core host membership is the trust boundary. Exactly one matching
        // store is required, so an alias can never select between stores.
        var matches = stores.Where(store => storeService.ContainsHostValue(store, requestHost.Host))
            .ToList();
        if (matches.Count != 1 ||
            !SitemapCanonicalOrigin.TryCreate(matches[0].Url, matches[0].Url, out canonicalBase))
        {
            return false;
        }

        if (requestHost.Host.Equals(canonicalBase.IdnHost, StringComparison.OrdinalIgnoreCase))
            hostKind = CanonicalHostKind.Canonical;
        else if (requestHost.Host.Equals($"www.{canonicalBase.IdnHost}",
                     StringComparison.OrdinalIgnoreCase))
            hostKind = CanonicalHostKind.WwwAlias;
        else
            hostKind = CanonicalHostKind.OtherRegisteredAlias;

        // The request port is deliberately ignored and never reflected.
        return true;
    }

    internal static string BuildCanonicalUrl(Uri canonicalBase, PathString requestPath,
        QueryString queryString)
    {
        ArgumentNullException.ThrowIfNull(canonicalBase);

        var trustedBase = canonicalBase.GetLeftPart(UriPartial.Path).TrimEnd('/');
        var escapedPath = requestPath.HasValue ? requestPath.ToUriComponent() : "/";
        if (!escapedPath.StartsWith('/'))
            escapedPath = $"/{escapedPath}";

        // QueryString.ToUriComponent retains valid catalog filters, paging,
        // search and campaign attribution without reparsing or decoding them.
        // It cannot influence the authority because it is appended only after
        // the trusted Store.Url-derived base and escaped request path.
        return $"{trustedBase}{escapedPath}{queryString.ToUriComponent()}";
    }

    private static string StripOptionalCulturePrefix(string path)
    {
        if (string.IsNullOrEmpty(path) || path[0] != '/')
            return path;

        var nextSlash = path.IndexOf('/', 1);
        if (nextSlash < 0)
            return path;

        var segment = path.AsSpan(1, nextSlash - 1);
        var isCulture = segment.Length == 2 && char.IsLetter(segment[0]) && char.IsLetter(segment[1]) ||
                        segment.Length == 5 && char.IsLetter(segment[0]) && char.IsLetter(segment[1]) &&
                        segment[2] == '-' && char.IsLetter(segment[3]) && char.IsLetter(segment[4]);
        return isCulture ? path[nextSlash..] : path;
    }

    private static bool HasPathPrefix(string path, string prefix) =>
        path.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith($"{prefix}/", StringComparison.OrdinalIgnoreCase);
}

internal enum CanonicalHostKind
{
    Invalid = 0,
    Canonical = 1,
    WwwAlias = 2,
    OtherRegisteredAlias = 3
}
