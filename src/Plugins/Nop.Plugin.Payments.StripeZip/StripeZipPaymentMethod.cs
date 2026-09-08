using Nop.Plugin.Misc.StripeBnplCore.Domain;
using Nop.Plugin.Misc.StripeBnplCore.Services;
using Nop.Plugin.Payments.StripeBnplShared;
using Nop.Services.Localization;

namespace Nop.Plugin.Payments.StripeZip;

public sealed class StripeZipPaymentMethod : StripeBnplPaymentMethodBase
{
    public StripeZipPaymentMethod(
        ILocalizationService localizationService,
        IStripeBnplService stripeBnplService)
        : base(localizationService, stripeBnplService)
    {
    }

    protected override BnplProvider Provider => BnplProvider.Zip;

    protected override string LocaleResourcePrefix => "Plugins.Payments.StripeZip";

    protected override string DefaultPaymentMethodDescription =>
        "Pay in 4 with Zip, if eligible. Zip shows any customer fee and all terms before you agree.";
}
