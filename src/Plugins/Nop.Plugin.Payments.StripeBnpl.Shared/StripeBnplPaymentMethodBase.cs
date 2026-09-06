using Microsoft.AspNetCore.Http;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Misc.StripeBnplCore.Domain;
using Nop.Plugin.Misc.StripeBnplCore.Services;
using Nop.Services.Localization;
using Nop.Services.Payments;
using Nop.Services.Plugins;

namespace Nop.Plugin.Payments.StripeBnplShared;

/// <summary>
/// Shared nopCommerce adapter for the provider-specific Stripe BNPL payment methods.
/// Stripe session creation, eligibility, configuration and webhook handling live in
/// Misc.StripeBnplCore so each provider remains a distinct checkout radio option.
/// </summary>
public abstract class StripeBnplPaymentMethodBase : BasePlugin, IPaymentMethod
{
    private readonly ILocalizationService _localizationService;
    private readonly IStripeBnplService _stripeBnplService;

    protected StripeBnplPaymentMethodBase(
        ILocalizationService localizationService,
        IStripeBnplService stripeBnplService)
    {
        _localizationService = localizationService;
        _stripeBnplService = stripeBnplService;
    }

    protected abstract BnplProvider Provider { get; }

    protected abstract string LocaleResourcePrefix { get; }

    protected abstract string DefaultPaymentMethodDescription { get; }

    public Task<ProcessPaymentResult> ProcessPaymentAsync(ProcessPaymentRequest processPaymentRequest)
    {
        ArgumentNullException.ThrowIfNull(processPaymentRequest);
        return Task.FromResult(new ProcessPaymentResult());
    }

    public async Task PostProcessPaymentAsync(PostProcessPaymentRequest postProcessPaymentRequest)
    {
        ArgumentNullException.ThrowIfNull(postProcessPaymentRequest);
        await _stripeBnplService.CreateCheckoutAndRedirectAsync(Provider, postProcessPaymentRequest);
    }

    public Task<bool> HidePaymentMethodAsync(IList<ShoppingCartItem> cart)
    {
        ArgumentNullException.ThrowIfNull(cart);
        return _stripeBnplService.ShouldHidePaymentMethodAsync(Provider, cart);
    }

    /// <summary>
    /// Provider-specific BNPL surcharges are intentionally forbidden. The Stripe fee is
    /// recorded by the core plugin for reporting and is never added to the customer total.
    /// </summary>
    public Task<decimal> GetAdditionalHandlingFeeAsync(IList<ShoppingCartItem> cart)
    {
        ArgumentNullException.ThrowIfNull(cart);
        return Task.FromResult(decimal.Zero);
    }

    public Task<CapturePaymentResult> CaptureAsync(CapturePaymentRequest capturePaymentRequest)
    {
        return Task.FromResult(new CapturePaymentResult { Errors = ["Capture is not supported for Stripe BNPL payments."] });
    }

    public Task<RefundPaymentResult> RefundAsync(RefundPaymentRequest refundPaymentRequest)
    {
        return Task.FromResult(new RefundPaymentResult { Errors = ["Start the refund from Stripe. The signed webhook will synchronize the order."] });
    }

    public Task<VoidPaymentResult> VoidAsync(VoidPaymentRequest voidPaymentRequest)
    {
        return Task.FromResult(new VoidPaymentResult { Errors = ["Void is not supported for Stripe BNPL payments."] });
    }

    public Task<ProcessPaymentResult> ProcessRecurringPaymentAsync(ProcessPaymentRequest processPaymentRequest)
    {
        return Task.FromResult(new ProcessPaymentResult { Errors = ["Recurring payments are not supported by this payment method."] });
    }

    public Task<CancelRecurringPaymentResult> CancelRecurringPaymentAsync(CancelRecurringPaymentRequest cancelPaymentRequest)
    {
        return Task.FromResult(new CancelRecurringPaymentResult { Errors = ["Recurring payments are not supported by this payment method."] });
    }

    public Task<bool> CanRePostProcessPaymentAsync(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);
        return _stripeBnplService.CanRePostProcessPaymentAsync(Provider, order);
    }

    public Task<IList<string>> ValidatePaymentFormAsync(IFormCollection form)
    {
        return Task.FromResult<IList<string>>([]);
    }

    public Task<ProcessPaymentRequest> GetPaymentInfoAsync(IFormCollection form)
    {
        return Task.FromResult(new ProcessPaymentRequest());
    }

    public override string GetConfigurationPageUrl()
    {
        return _stripeBnplService.GetConfigurationPageUrl(Provider);
    }

    public Type GetPublicViewComponent()
    {
        // nopCommerce does not request a component when SkipPaymentInfo is true.
        return null;
    }

    public async Task<string> GetPaymentMethodDescriptionAsync()
    {
        return await _localizationService.GetResourceAsync($"{LocaleResourcePrefix}.PaymentMethodDescription");
    }

    public override async Task InstallAsync()
    {
        await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
        {
            [$"{LocaleResourcePrefix}.PaymentMethodDescription"] = DefaultPaymentMethodDescription
        });

        await base.InstallAsync();
    }

    public override async Task UninstallAsync()
    {
        await _localizationService.DeleteLocaleResourcesAsync(LocaleResourcePrefix);
        await base.UninstallAsync();
    }

    public bool SupportCapture => false;

    public bool SupportPartiallyRefund => false;

    public bool SupportRefund => false;

    public bool SupportVoid => false;

    public RecurringPaymentType RecurringPaymentType => RecurringPaymentType.NotSupported;

    public PaymentMethodType PaymentMethodType => PaymentMethodType.Redirection;

    public bool SkipPaymentInfo => true;
}
