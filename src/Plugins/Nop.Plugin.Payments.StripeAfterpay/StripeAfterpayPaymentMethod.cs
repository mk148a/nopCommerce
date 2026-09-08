using Nop.Plugin.Misc.StripeBnplCore.Domain;
using Nop.Plugin.Misc.StripeBnplCore.Services;
using Nop.Plugin.Payments.StripeBnplShared;
using Nop.Services.Localization;

namespace Nop.Plugin.Payments.StripeAfterpay;

public sealed class StripeAfterpayPaymentMethod : StripeBnplPaymentMethodBase
{
    public StripeAfterpayPaymentMethod(
        ILocalizationService localizationService,
        IStripeBnplService stripeBnplService)
        : base(localizationService, stripeBnplService)
    {
    }

    protected override BnplProvider Provider => BnplProvider.Afterpay;

    protected override string LocaleResourcePrefix => "Plugins.Payments.StripeAfterpay";

    protected override string DefaultPaymentMethodDescription =>
        "Split your purchase into installments, if eligible. Available plans and terms depend on your country and provider approval.";
}
