using Nop.Plugin.Misc.StripeBnplCore.Domain;
using Nop.Plugin.Misc.StripeBnplCore.Services;
using Nop.Plugin.Payments.StripeBnplShared;
using Nop.Services.Localization;

namespace Nop.Plugin.Payments.StripeAffirm;

public sealed class StripeAffirmPaymentMethod : StripeBnplPaymentMethodBase
{
    public StripeAffirmPaymentMethod(
        ILocalizationService localizationService,
        IStripeBnplService stripeBnplService)
        : base(localizationService, stripeBnplService)
    {
    }

    protected override BnplProvider Provider => BnplProvider.Affirm;

    protected override string LocaleResourcePrefix => "Plugins.Payments.StripeAffirm";

    protected override string DefaultPaymentMethodDescription =>
        "Pay in 4 or choose monthly payments, if eligible. Affirm shows available rates and terms before you agree.";
}
