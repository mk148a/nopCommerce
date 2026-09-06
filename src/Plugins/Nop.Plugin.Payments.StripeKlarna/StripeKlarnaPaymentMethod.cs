using Nop.Plugin.Misc.StripeBnplCore.Domain;
using Nop.Plugin.Misc.StripeBnplCore.Services;
using Nop.Plugin.Payments.StripeBnplShared;
using Nop.Services.Localization;

namespace Nop.Plugin.Payments.StripeKlarna;

public sealed class StripeKlarnaPaymentMethod : StripeBnplPaymentMethodBase
{
    public StripeKlarnaPaymentMethod(
        ILocalizationService localizationService,
        IStripeBnplService stripeBnplService)
        : base(localizationService, stripeBnplService)
    {
    }

    protected override BnplProvider Provider => BnplProvider.Klarna;

    protected override string LocaleResourcePrefix => "Plugins.Payments.StripeKlarna";

    protected override string DefaultPaymentMethodDescription =>
        "Pay now or pay over time with Klarna. Available options depend on your location, order amount, and Klarna approval.";
}
