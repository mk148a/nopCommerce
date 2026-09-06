using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Nop.Plugin.Widgets.GoogleAnalytics.Services;

public sealed record GoogleAnalyticsPurchaseDispatchConfirmation(string Url, string RequestVerificationToken,
    string RequestVerificationFieldName);

public interface IGoogleAnalyticsPurchaseDispatchConfirmationFactory
{
    GoogleAnalyticsPurchaseDispatchConfirmation Create();
}

/// <summary>
/// Builds a same-origin, PathBase-aware confirmation URL and supplies the
/// antiforgery request token which is posted in the confirmation form body.
/// </summary>
public sealed class GoogleAnalyticsPurchaseDispatchConfirmationFactory : IGoogleAnalyticsPurchaseDispatchConfirmationFactory
{
    private readonly IAntiforgery _antiforgery;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly LinkGenerator _linkGenerator;

    public GoogleAnalyticsPurchaseDispatchConfirmationFactory(IAntiforgery antiforgery,
        IHttpContextAccessor httpContextAccessor, LinkGenerator linkGenerator)
    {
        _antiforgery = antiforgery;
        _httpContextAccessor = httpContextAccessor;
        _linkGenerator = linkGenerator;
    }

    public GoogleAnalyticsPurchaseDispatchConfirmation Create()
    {
        var httpContext = _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("A purchase dispatch confirmation requires an HTTP context.");
        var url = _linkGenerator.GetPathByRouteValues(httpContext,
            GoogleAnalyticsDefaults.PurchaseDispatchConfirmationRouteName, values: null,
            pathBase: httpContext.Request.PathBase);
        var antiforgeryTokens = _antiforgery.GetAndStoreTokens(httpContext);
        var requestToken = antiforgeryTokens.RequestToken;
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(requestToken) ||
            string.IsNullOrWhiteSpace(antiforgeryTokens.FormFieldName))
            throw new InvalidOperationException("The purchase dispatch confirmation route or antiforgery token is unavailable.");

        return new(url, requestToken, antiforgeryTokens.FormFieldName);
    }
}
