using Nop.Core;

namespace Nop.Plugin.Misc.StripeBnplCore.Domain;

public class StripeBnplCheckoutSession : BaseEntity
{
    public int OrderId { get; set; }
    public int ProviderId { get; set; }
    public string SessionId { get; set; }
    public string CheckoutUrl { get; set; }
    public string PaymentIntentId { get; set; }
    public long AmountMinor { get; set; }
    public string Currency { get; set; }
    public string Status { get; set; }
    public int AttemptNumber { get; set; }
    public string IdempotencyKey { get; set; }
    public string EligibilitySnapshotJson { get; set; }
    public string EligibilitySnapshotHash { get; set; }
    public DateTime ExpiresOnUtc { get; set; }
    public bool IsSandbox { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime UpdatedOnUtc { get; set; }

    public BnplProvider Provider
    {
        get => (BnplProvider)ProviderId;
        set => ProviderId = (int)value;
    }
}
