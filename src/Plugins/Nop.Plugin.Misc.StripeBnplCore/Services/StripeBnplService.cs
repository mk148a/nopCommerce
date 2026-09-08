using Microsoft.AspNetCore.Http;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Misc.StripeBnplCore.Domain;
using Nop.Services.Payments;

namespace Nop.Plugin.Misc.StripeBnplCore.Services;

public sealed class StripeBnplService : IStripeBnplService
{
    private readonly IBnplEligibilityService _eligibilityService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IStripeBnplCheckoutService _checkoutService;
    private readonly IStripeBnplConfigurationClient _configurationClient;
    private readonly StripeBnplSettings _settings;
    private readonly IWebHelper _webHelper;

    public StripeBnplService(
        IBnplEligibilityService eligibilityService,
        IHttpContextAccessor httpContextAccessor,
        IStripeBnplCheckoutService checkoutService,
        IStripeBnplConfigurationClient configurationClient,
        StripeBnplSettings settings,
        IWebHelper webHelper)
    {
        _eligibilityService = eligibilityService;
        _httpContextAccessor = httpContextAccessor;
        _checkoutService = checkoutService;
        _configurationClient = configurationClient;
        _settings = settings;
        _webHelper = webHelper;
    }

    public Task<BnplCartEligibilityResult> EvaluateCartAsync(BnplProvider provider, IList<ShoppingCartItem> cart) =>
        _eligibilityService.EvaluateCartAsync(provider, cart);

    public async Task<bool> ShouldHidePaymentMethodAsync(BnplProvider provider, IList<ShoppingCartItem> cart)
    {
        if (!(await _eligibilityService.EvaluateCartAsync(provider, cart)).IsEligible)
            return true;
        var health = await _configurationClient.CheckProviderOnlyAsync(provider,
            _settings.GetPaymentMethodConfigurationId(provider), expectedLivemode: !_settings.UseSandbox);
        return !health.IsHealthy;
    }

    public async Task CreateCheckoutAndRedirectAsync(BnplProvider provider, PostProcessPaymentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Order);
        var result = await _checkoutService.CreateAsync(provider, request.Order);
        if (!Uri.TryCreate(result.Url, UriKind.Absolute, out var checkoutUri) ||
            checkoutUri.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(checkoutUri.Host, "checkout.stripe.com", StringComparison.OrdinalIgnoreCase))
            throw new NopException("Stripe Checkout returned an invalid redirect URL.");

        var response = _httpContextAccessor.HttpContext?.Response
            ?? throw new NopException("HTTP response is unavailable for Stripe Checkout redirect.");
        response.Redirect(result.Url);
    }

    public Task<bool> CanRePostProcessPaymentAsync(BnplProvider provider, Order order)
    {
        var canRetry = order != null && !order.Deleted && order.OrderStatus != OrderStatus.Cancelled &&
                       order.PaymentStatus == PaymentStatus.Pending &&
                       string.Equals(order.PaymentMethodSystemName, StripeBnplDefaults.GetSystemName(provider),
                           StringComparison.OrdinalIgnoreCase);
        return Task.FromResult(canRetry);
    }

    public string GetConfigurationPageUrl(BnplProvider provider) =>
        $"{_webHelper.GetStoreLocation().TrimEnd('/')}/Admin/StripeBnpl/Configure?provider={(int)provider}";
}
