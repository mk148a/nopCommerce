using Nop.Core;

namespace Nop.Plugin.Misc.StripeBnplCore.Domain;

public class StripeBnplPaymentRecord : BaseEntity
{
    public int OrderId { get; set; }
    public int ProviderId { get; set; }
    public string SessionId { get; set; }
    public string PaymentIntentId { get; set; }
    public string ChargeId { get; set; }
    public string BalanceTransactionId { get; set; }
    public string PaymentMethodType { get; set; }
    public string Status { get; set; }
    public long AmountMinor { get; set; }
    public long? FeeMinor { get; set; }
    public long? NetMinor { get; set; }
    public long RefundedAmountMinor { get; set; }
    public long? PendingRefundAmountMinor { get; set; }
    public DateTime? RefundClaimedOnUtc { get; set; }
    public string Currency { get; set; }
    public string SettlementCurrency { get; set; }
    public decimal? ExchangeRate { get; set; }
    public string FeeDetailsJson { get; set; }
    public string FeeDataStatus { get; set; }
    public string FeeDataError { get; set; }
    public int FeeReconciliationAttemptCount { get; set; }
    public DateTime? FeeLastAttemptOnUtc { get; set; }
    public DateTime? FeeCompletedOnUtc { get; set; }
    public bool IsSandbox { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime UpdatedOnUtc { get; set; }

    public BnplProvider Provider
    {
        get => (BnplProvider)ProviderId;
        set => ProviderId = (int)value;
    }
}
