using Nop.Core.Configuration;
using Nop.Plugin.Misc.StripeBnplCore.Domain;

namespace Nop.Plugin.Misc.StripeBnplCore;

public class StripeBnplSettings : ISettings
{
    public bool UseSandbox { get; set; } = true;
    // Kept only so an older persisted setting can be read without breaking an upgrade.
    // Live approval is a non-configurable safety invariant; runtime code must never
    // use this legacy value to bypass the provider approval reference requirement.
    [Obsolete("Live written approval is always required and cannot be disabled.")]
    public bool RequireWrittenApprovalForLive { get; set; } = true;
    public string SandboxDatabaseName { get; set; } = "HoodArcheryShopStripeSandboxDb";
    public string SandboxDatabaseServer { get; set; }
    public string LiveDatabaseName { get; set; }
    public string LiveDatabaseServer { get; set; }
    public string StripeAccountCountryIso2 { get; set; } = "US";
    public string LiveRestrictedKey { get; set; }
    public string TestRestrictedKey { get; set; }
    public string LiveWebhookSecret { get; set; }
    public string TestWebhookSecret { get; set; }
    public string LiveKlarnaConfigurationId { get; set; }
    public string TestKlarnaConfigurationId { get; set; }
    public string LiveAffirmConfigurationId { get; set; }
    public string TestAffirmConfigurationId { get; set; }
    public string LiveAfterpayConfigurationId { get; set; }
    public string TestAfterpayConfigurationId { get; set; }
    public string LiveZipConfigurationId { get; set; }
    public string TestZipConfigurationId { get; set; }
    public string KlarnaApprovalReference { get; set; }
    public string AffirmApprovalReference { get; set; }
    public string AfterpayApprovalReference { get; set; }
    public string ZipApprovalReference { get; set; }
    // This is a merchant-owned guard, not an Afterpay policy limit. Zero keeps
    // the account-level Stripe/partner review authoritative for fulfillment.
    public int AfterpayMaximumFulfillmentDays { get; set; }
    public int PendingOrderCancellationHours { get; set; } = 24;
    public int WebhookSignatureToleranceSeconds { get; set; } = 300;
    public decimal KlarnaMinimumAmount { get; set; } = 1m;
    public decimal KlarnaMaximumAmount { get; set; } = 10000m;
    public decimal AffirmMinimumAmount { get; set; } = 35m;
    public decimal AffirmMaximumAmount { get; set; } = 30000m;
    public decimal AfterpayMinimumAmount { get; set; } = 1m;
    public decimal AfterpayMaximumAmount { get; set; } = 4000m;
    public decimal ZipMinimumAmount { get; set; } = 35m;
    public decimal ZipMaximumAmount { get; set; } = 1500m;

    public string GetActiveRestrictedKey() => UseSandbox ? TestRestrictedKey : LiveRestrictedKey;
    public string GetActiveWebhookSecret() => UseSandbox ? TestWebhookSecret : LiveWebhookSecret;

    public string GetPaymentMethodConfigurationId(BnplProvider provider)
    {
        return (UseSandbox, provider) switch
        {
            (true, BnplProvider.Klarna) => TestKlarnaConfigurationId,
            (true, BnplProvider.Affirm) => TestAffirmConfigurationId,
            (true, BnplProvider.Afterpay) => TestAfterpayConfigurationId,
            (true, BnplProvider.Zip) => TestZipConfigurationId,
            (false, BnplProvider.Klarna) => LiveKlarnaConfigurationId,
            (false, BnplProvider.Affirm) => LiveAffirmConfigurationId,
            (false, BnplProvider.Afterpay) => LiveAfterpayConfigurationId,
            (false, BnplProvider.Zip) => LiveZipConfigurationId,
            _ => null
        };
    }

    public string GetApprovalReference(BnplProvider provider)
    {
        return provider switch
        {
            BnplProvider.Klarna => KlarnaApprovalReference,
            BnplProvider.Affirm => AffirmApprovalReference,
            BnplProvider.Afterpay => AfterpayApprovalReference,
            BnplProvider.Zip => ZipApprovalReference,
            _ => null
        };
    }

    public (decimal Minimum, decimal Maximum) GetAmountRange(BnplProvider provider) => provider switch
    {
        BnplProvider.Klarna => (KlarnaMinimumAmount, KlarnaMaximumAmount),
        BnplProvider.Affirm => (AffirmMinimumAmount, AffirmMaximumAmount),
        BnplProvider.Afterpay => (AfterpayMinimumAmount, AfterpayMaximumAmount),
        BnplProvider.Zip => (ZipMinimumAmount, ZipMaximumAmount),
        _ => (decimal.MaxValue, decimal.MinValue)
    };
}
