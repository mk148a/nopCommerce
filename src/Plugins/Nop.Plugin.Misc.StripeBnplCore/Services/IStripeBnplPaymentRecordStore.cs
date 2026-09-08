using Nop.Plugin.Misc.StripeBnplCore.Domain;

namespace Nop.Plugin.Misc.StripeBnplCore.Services;

public sealed record StripeBnplPaymentSnapshot(
    int OrderId,
    BnplProvider Provider,
    string SessionId,
    string PaymentIntentId,
    string ChargeId,
    string BalanceTransactionId,
    string PaymentMethodType,
    string Status,
    long AmountMinor,
    long? FeeMinor,
    long? NetMinor,
    string Currency,
    string SettlementCurrency,
    decimal? ExchangeRate,
    string FeeDetailsJson,
    string FeeDataStatus,
    bool IsSandbox);

public sealed record StripeBnplFeeSnapshot(
    string ChargeId,
    string BalanceTransactionId,
    long FeeMinor,
    long NetMinor,
    string SettlementCurrency,
    decimal? ExchangeRate,
    string FeeDetailsJson);

public sealed record StripeBnplCostReportQuery(
    int? OrderId,
    BnplProvider? Provider,
    bool? IsSandbox,
    string FeeDataStatus,
    int PageIndex,
    int PageSize);

public sealed record StripeBnplCostReportPage(
    IReadOnlyList<StripeBnplPaymentRecord> Records,
    int TotalCount);

public interface IStripeBnplPaymentRecordStore
{
    Task<StripeBnplFinalizationClaim> TryAcquireFinalizationAsync(StripeBnplPaymentSnapshot snapshot);
    Task<StripeBnplPaymentRecord> GetByOrderIdAsync(int orderId);
    Task<StripeBnplPaymentRecord> GetByChargeIdAsync(string chargeId);
    Task<StripeBnplPaymentRecord> GetByPaymentIntentIdAsync(string paymentIntentId);
    Task<StripeBnplCostReportPage> SearchCostReportAsync(StripeBnplCostReportQuery query);
    Task<IList<StripeBnplPaymentRecord>> GetFeeReconciliationCandidatesAsync(int maxCount,
        DateTime retryBeforeUtc, int maxAttempts);
    Task<StripeBnplFeeReconciliationClaim> TryAcquireFeeReconciliationAsync(int recordId,
        DateTime staleBeforeUtc, int maxAttempts);
    Task CompleteFeeReconciliationAsync(int recordId, StripeBnplFeeSnapshot snapshot);
    Task FailFeeReconciliationAsync(int recordId, string error);
    Task UpsertAsync(StripeBnplPaymentSnapshot snapshot);
    Task CompleteFinalizationAsync(StripeBnplPaymentSnapshot snapshot);
    Task FailFinalizationAsync(int orderId, string errorStatus = "finalization_failed");
    Task<StripeBnplRefundClaim> TryAcquireRefundAsync(int orderId, long cumulativeRefundedAmountMinor);
    Task CompleteRefundAsync(int orderId, long cumulativeRefundedAmountMinor, string status);
    Task FailRefundAsync(int orderId, long cumulativeRefundedAmountMinor);
    Task UpdateStatusAsync(StripeBnplPaymentRecord record, string status);
}

public enum StripeBnplFinalizationClaim
{
    Acquired,
    AlreadyPaid,
    InProgress,
    Terminal
}

public enum StripeBnplRefundClaim
{
    Acquired,
    AlreadyApplied,
    InProgress
}

public enum StripeBnplFeeReconciliationClaim
{
    Acquired,
    AlreadyComplete,
    InProgress,
    Exhausted
}
