using Nop.Plugin.Misc.StripeBnplCore.Domain;

namespace Nop.Plugin.Misc.StripeBnplCore;

public static class StripeBnplDefaults
{
    public const string SystemName = "Misc.StripeBnplCore";
    // Stripe.net 52.2.0 pins all requests to this API version. Recent Stripe.net
    // releases intentionally removed per-client version overrides.
    public const string StripeApiVersion = "2026-07-29.dahlia";
    public const string WebhookRouteName = "Misc.StripeBnplCore.Webhook";
    public const string ReturnRouteName = "Misc.StripeBnplCore.Return";
    public const string CancelRouteName = "Misc.StripeBnplCore.Cancel";
    public const string StatusRouteName = "Misc.StripeBnplCore.Status";
    public const string WebhookPath = "stripe-bnpl/webhook";
    public const string ReturnPath = "stripe-bnpl/return";
    public const string CancelPath = "stripe-bnpl/cancel";
    public const string StatusPath = "stripe-bnpl/status";
    public const string FeeReconciliationTaskType =
        "Nop.Plugin.Misc.StripeBnplCore.Services.StripeBnplFeeReconciliationTask, Nop.Plugin.Misc.StripeBnplCore";
    public const string PendingOrderCleanupTaskType =
        "Nop.Plugin.Misc.StripeBnplCore.Services.StripeBnplPendingOrderCleanupTask, Nop.Plugin.Misc.StripeBnplCore";

    public static readonly IReadOnlyCollection<string> WebhookEvents = new[]
    {
        "checkout.session.completed",
        "checkout.session.async_payment_succeeded",
        "checkout.session.async_payment_failed",
        "checkout.session.expired",
        "payment_intent.succeeded",
        "payment_intent.payment_failed",
        "payment_intent.canceled",
        "charge.refunded",
        "charge.dispute.created",
        "charge.dispute.closed"
    };

    public static string GetProviderSlug(BnplProvider provider) => provider switch
    {
        BnplProvider.Klarna => "klarna",
        BnplProvider.Affirm => "affirm",
        BnplProvider.Afterpay => "afterpay_clearpay",
        BnplProvider.Zip => "zip",
        _ => throw new ArgumentOutOfRangeException(nameof(provider))
    };

    public static string GetSystemName(BnplProvider provider) => provider switch
    {
        BnplProvider.Klarna => "Payments.StripeKlarna",
        BnplProvider.Affirm => "Payments.StripeAffirm",
        BnplProvider.Afterpay => "Payments.StripeAfterpay",
        BnplProvider.Zip => "Payments.StripeZip",
        _ => throw new ArgumentOutOfRangeException(nameof(provider))
    };

    public static bool TryParseProviderSlug(string value, out BnplProvider provider)
    {
        provider = value?.Trim().ToLowerInvariant() switch
        {
            "klarna" => BnplProvider.Klarna,
            "affirm" => BnplProvider.Affirm,
            "afterpay" or "afterpay_clearpay" or "clearpay" => BnplProvider.Afterpay,
            "zip" => BnplProvider.Zip,
            _ => 0
        };
        return provider != 0;
    }
}
