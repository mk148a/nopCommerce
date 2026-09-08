using LinqToDB;
using Nop.Data;
using Nop.Plugin.Misc.StripeBnplCore.Domain;

namespace Nop.Plugin.Misc.StripeBnplCore.Services;

public sealed class StripeBnplPaymentRecordStore : IStripeBnplPaymentRecordStore
{
    private readonly IRepository<StripeBnplPaymentRecord> _repository;

    public StripeBnplPaymentRecordStore(IRepository<StripeBnplPaymentRecord> repository)
    {
        _repository = repository;
    }

    public Task<StripeBnplPaymentRecord> GetByOrderIdAsync(int orderId) =>
        _repository.Table.FirstOrDefaultAsync(item => item.OrderId == orderId);

    public Task<StripeBnplPaymentRecord> GetByChargeIdAsync(string chargeId) =>
        string.IsNullOrWhiteSpace(chargeId)
            ? Task.FromResult<StripeBnplPaymentRecord>(null)
            : _repository.Table.FirstOrDefaultAsync(item => item.ChargeId == chargeId);

    public Task<StripeBnplPaymentRecord> GetByPaymentIntentIdAsync(string paymentIntentId) =>
        string.IsNullOrWhiteSpace(paymentIntentId)
            ? Task.FromResult<StripeBnplPaymentRecord>(null)
            : _repository.Table.FirstOrDefaultAsync(item => item.PaymentIntentId == paymentIntentId);

    public async Task<StripeBnplCostReportPage> SearchCostReportAsync(StripeBnplCostReportQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.PageIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(query), "The page index cannot be negative.");
        if (query.PageSize is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(query), "The page size must be between 1 and 100.");
        if (query.OrderId is <= 0)
            throw new ArgumentOutOfRangeException(nameof(query), "The order ID must be positive.");
        if (query.Provider.HasValue && !Enum.IsDefined(query.Provider.Value))
            throw new ArgumentOutOfRangeException(nameof(query), "The BNPL provider is invalid.");

        var feeDataStatus = query.FeeDataStatus?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(feeDataStatus) &&
            feeDataStatus is not StripeBnplFeeDataStatus.Pending and
                not StripeBnplFeeDataStatus.Reconciling and
                not StripeBnplFeeDataStatus.Complete and
                not StripeBnplFeeDataStatus.Error)
            throw new ArgumentException("The fee reconciliation status is invalid.", nameof(query));

        var filtered = _repository.Table;
        if (query.OrderId.HasValue)
            filtered = filtered.Where(item => item.OrderId == query.OrderId.Value);
        if (query.Provider.HasValue)
            filtered = filtered.Where(item => item.ProviderId == (int)query.Provider.Value);
        if (query.IsSandbox.HasValue)
            filtered = filtered.Where(item => item.IsSandbox == query.IsSandbox.Value);
        if (!string.IsNullOrWhiteSpace(feeDataStatus))
            filtered = filtered.Where(item => item.FeeDataStatus == feeDataStatus);

        var totalCount = await LinqToDB.AsyncExtensions.CountAsync(filtered);
        var pageQuery = filtered
            .OrderByDescending(item => item.CreatedOnUtc)
            .ThenByDescending(item => item.Id)
            .Skip(query.PageIndex * query.PageSize)
            .Take(query.PageSize);
        var records = await LinqToDB.AsyncExtensions.ToListAsync(pageQuery);

        return new StripeBnplCostReportPage(records, totalCount);
    }

    public Task<IList<StripeBnplPaymentRecord>> GetFeeReconciliationCandidatesAsync(int maxCount,
        DateTime retryBeforeUtc, int maxAttempts)
    {
        if (maxCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxCount));
        if (maxAttempts <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxAttempts));

        return _repository.GetAllAsync(query => query
            .Where(item => item.FeeReconciliationAttemptCount < maxAttempts &&
                           (item.Status == "paid" || item.Status == "partially_refunded" ||
                            item.Status == "refunded" || item.Status.StartsWith("disputed_") ||
                            item.Status.StartsWith("dispute_closed_")) &&
                           (item.FeeDataStatus == StripeBnplFeeDataStatus.Pending ||
                            item.FeeDataStatus == StripeBnplFeeDataStatus.Error ||
                            item.FeeDataStatus == StripeBnplFeeDataStatus.Reconciling) &&
                           (!item.FeeLastAttemptOnUtc.HasValue || item.FeeLastAttemptOnUtc <= retryBeforeUtc))
            .OrderBy(item => item.FeeLastAttemptOnUtc)
            .ThenBy(item => item.Id)
            .Take(maxCount));
    }

    public async Task<StripeBnplFeeReconciliationClaim> TryAcquireFeeReconciliationAsync(int recordId,
        DateTime staleBeforeUtc, int maxAttempts)
    {
        if (recordId <= 0)
            throw new ArgumentOutOfRangeException(nameof(recordId));
        if (maxAttempts <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxAttempts));

        var record = await _repository.GetByIdAsync(recordId);
        if (record == null)
            return StripeBnplFeeReconciliationClaim.Exhausted;
        if (string.Equals(record.FeeDataStatus, StripeBnplFeeDataStatus.Complete,
                StringComparison.OrdinalIgnoreCase))
            return StripeBnplFeeReconciliationClaim.AlreadyComplete;
        if (record.FeeReconciliationAttemptCount >= maxAttempts)
            return StripeBnplFeeReconciliationClaim.Exhausted;
        if (string.Equals(record.FeeDataStatus, StripeBnplFeeDataStatus.Reconciling,
                StringComparison.OrdinalIgnoreCase) && record.FeeLastAttemptOnUtc > staleBeforeUtc)
            return StripeBnplFeeReconciliationClaim.InProgress;

        var now = DateTime.UtcNow;
        var previousStatus = record.FeeDataStatus;
        var previousAttempt = record.FeeReconciliationAttemptCount;
        var previousAttemptOnUtc = record.FeeLastAttemptOnUtc;
        var affected = await _repository.Table
            .Where(item => item.Id == record.Id && item.FeeDataStatus == previousStatus &&
                           item.FeeReconciliationAttemptCount == previousAttempt &&
                           item.FeeLastAttemptOnUtc == previousAttemptOnUtc)
            .Set(item => item.FeeDataStatus, StripeBnplFeeDataStatus.Reconciling)
            .Set(item => item.FeeDataError, (string)null)
            .Set(item => item.FeeReconciliationAttemptCount, previousAttempt + 1)
            .Set(item => item.FeeLastAttemptOnUtc, (DateTime?)now)
            .Set(item => item.UpdatedOnUtc, now)
            .UpdateAsync();
        return affected == 1
            ? StripeBnplFeeReconciliationClaim.Acquired
            : StripeBnplFeeReconciliationClaim.InProgress;
    }

    public async Task CompleteFeeReconciliationAsync(int recordId, StripeBnplFeeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (recordId <= 0 || string.IsNullOrWhiteSpace(snapshot.BalanceTransactionId))
            throw new ArgumentException("A payment record and BalanceTransaction are required.");

        var now = DateTime.UtcNow;
        var affected = await _repository.Table
            .Where(item => item.Id == recordId &&
                           item.FeeDataStatus == StripeBnplFeeDataStatus.Reconciling)
            .Set(item => item.ChargeId, snapshot.ChargeId)
            .Set(item => item.BalanceTransactionId, snapshot.BalanceTransactionId)
            .Set(item => item.FeeMinor, (long?)snapshot.FeeMinor)
            .Set(item => item.NetMinor, (long?)snapshot.NetMinor)
            .Set(item => item.SettlementCurrency, snapshot.SettlementCurrency)
            .Set(item => item.ExchangeRate, snapshot.ExchangeRate)
            .Set(item => item.FeeDetailsJson, snapshot.FeeDetailsJson)
            .Set(item => item.FeeDataStatus, StripeBnplFeeDataStatus.Complete)
            .Set(item => item.FeeDataError, (string)null)
            .Set(item => item.FeeCompletedOnUtc, (DateTime?)now)
            .Set(item => item.UpdatedOnUtc, now)
            .UpdateAsync();
        if (affected == 1)
            return;

        var record = await _repository.GetByIdAsync(recordId);
        if (!string.Equals(record?.FeeDataStatus, StripeBnplFeeDataStatus.Complete,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Stripe BNPL fee reconciliation claim no longer belongs to this worker.");
    }

    public async Task FailFeeReconciliationAsync(int recordId, string error)
    {
        if (recordId <= 0)
            return;
        var safeError = string.IsNullOrWhiteSpace(error)
            ? "Stripe fee data is not available yet."
            : error.Trim();
        if (safeError.Length > 1000)
            safeError = safeError[..1000];

        var now = DateTime.UtcNow;
        await _repository.Table
            .Where(item => item.Id == recordId &&
                           item.FeeDataStatus == StripeBnplFeeDataStatus.Reconciling)
            .Set(item => item.FeeDataStatus, StripeBnplFeeDataStatus.Error)
            .Set(item => item.FeeDataError, safeError)
            .Set(item => item.UpdatedOnUtc, now)
            .UpdateAsync();
    }

    public async Task<StripeBnplFinalizationClaim> TryAcquireFinalizationAsync(StripeBnplPaymentSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var now = DateTime.UtcNow;
        var existing = await GetByOrderIdAsync(snapshot.OrderId);
        if (existing == null)
        {
            try
            {
                await _repository.InsertAsync(ToEntity(snapshot, "finalizing", now), publishEvent: false);
                return StripeBnplFinalizationClaim.Acquired;
            }
            catch
            {
                existing = await GetByOrderIdAsync(snapshot.OrderId);
                if (existing == null)
                    throw;
            }
        }

        if (string.Equals(existing.Status, "paid", StringComparison.OrdinalIgnoreCase))
            return StripeBnplFinalizationClaim.AlreadyPaid;
        if (IsTerminalLifecycleStatus(existing.Status))
            return StripeBnplFinalizationClaim.Terminal;

        var canReclaim = !string.Equals(existing.Status, "finalizing", StringComparison.OrdinalIgnoreCase) ||
                         existing.UpdatedOnUtc <= now.AddMinutes(-5);
        if (!canReclaim)
            return StripeBnplFinalizationClaim.InProgress;

        var affected = await _repository.Table
            .Where(item => item.Id == existing.Id && item.Status == existing.Status &&
                           item.UpdatedOnUtc == existing.UpdatedOnUtc)
            .Set(item => item.Status, "finalizing")
            .Set(item => item.UpdatedOnUtc, now)
            .UpdateAsync();
        if (affected != 1)
            return StripeBnplFinalizationClaim.InProgress;

        await ApplySnapshotAsync(existing, snapshot, "finalizing");
        return StripeBnplFinalizationClaim.Acquired;
    }

    public async Task UpsertAsync(StripeBnplPaymentSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var record = await GetByOrderIdAsync(snapshot.OrderId);
        var now = DateTime.UtcNow;
        if (record == null)
        {
            await _repository.InsertAsync(new StripeBnplPaymentRecord
            {
                OrderId = snapshot.OrderId,
                Provider = snapshot.Provider,
                SessionId = snapshot.SessionId,
                PaymentIntentId = snapshot.PaymentIntentId,
                ChargeId = snapshot.ChargeId,
                BalanceTransactionId = snapshot.BalanceTransactionId,
                PaymentMethodType = snapshot.PaymentMethodType,
                Status = snapshot.Status,
                AmountMinor = snapshot.AmountMinor,
                FeeMinor = snapshot.FeeMinor,
                NetMinor = snapshot.NetMinor,
                RefundedAmountMinor = 0,
                Currency = snapshot.Currency,
                SettlementCurrency = snapshot.SettlementCurrency,
                ExchangeRate = snapshot.ExchangeRate,
                FeeDetailsJson = snapshot.FeeDetailsJson,
                FeeDataStatus = NormalizeFeeDataStatus(snapshot),
                FeeReconciliationAttemptCount = 0,
                FeeCompletedOnUtc = IsCompleteFeeSnapshot(snapshot) ? now : null,
                IsSandbox = snapshot.IsSandbox,
                CreatedOnUtc = now,
                UpdatedOnUtc = now
            }, publishEvent: false);
            return;
        }

        record.Provider = snapshot.Provider;
        record.SessionId = snapshot.SessionId;
        record.PaymentIntentId = snapshot.PaymentIntentId;
        record.ChargeId = snapshot.ChargeId;
        record.BalanceTransactionId = snapshot.BalanceTransactionId;
        record.PaymentMethodType = snapshot.PaymentMethodType;
        record.Status = SelectMonotonicLifecycleStatus(record.Status, snapshot.Status);
        record.AmountMinor = snapshot.AmountMinor;
        record.Currency = snapshot.Currency;
        ApplyFeeSnapshot(record, snapshot, now);
        record.IsSandbox = snapshot.IsSandbox;
        record.UpdatedOnUtc = now;
        await _repository.UpdateAsync(record, publishEvent: false);
    }

    public async Task CompleteFinalizationAsync(StripeBnplPaymentSnapshot snapshot)
    {
        var record = await GetByOrderIdAsync(snapshot.OrderId)
            ?? throw new InvalidOperationException("Stripe BNPL finalization claim was not found.");
        await ApplySnapshotAsync(record, snapshot, "paid");
    }

    public async Task FailFinalizationAsync(int orderId, string errorStatus = "finalization_failed")
    {
        var record = await GetByOrderIdAsync(orderId);
        if (record == null || !string.Equals(record.Status, "finalizing", StringComparison.OrdinalIgnoreCase))
            return;
        await UpdateStatusAsync(record, errorStatus);
    }

    public async Task<StripeBnplRefundClaim> TryAcquireRefundAsync(int orderId, long cumulativeRefundedAmountMinor)
    {
        if (orderId <= 0 || cumulativeRefundedAmountMinor <= 0)
            throw new ArgumentOutOfRangeException(nameof(cumulativeRefundedAmountMinor));

        var now = DateTime.UtcNow;
        var record = await GetByOrderIdAsync(orderId)
            ?? throw new InvalidOperationException("Stripe BNPL payment record was not found for refund processing.");
        if (record.RefundedAmountMinor >= cumulativeRefundedAmountMinor)
            return StripeBnplRefundClaim.AlreadyApplied;
        if (record.PendingRefundAmountMinor.HasValue && record.RefundClaimedOnUtc > now.AddMinutes(-5))
            return StripeBnplRefundClaim.InProgress;

        var affected = await _repository.Table
            .Where(item => item.Id == record.Id && item.UpdatedOnUtc == record.UpdatedOnUtc &&
                           item.RefundedAmountMinor == record.RefundedAmountMinor &&
                           item.PendingRefundAmountMinor == record.PendingRefundAmountMinor)
            .Set(item => item.PendingRefundAmountMinor, (long?)cumulativeRefundedAmountMinor)
            .Set(item => item.RefundClaimedOnUtc, (DateTime?)now)
            .Set(item => item.UpdatedOnUtc, now)
            .UpdateAsync();
        return affected == 1 ? StripeBnplRefundClaim.Acquired : StripeBnplRefundClaim.InProgress;
    }

    public async Task CompleteRefundAsync(int orderId, long cumulativeRefundedAmountMinor, string status)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var record = await GetByOrderIdAsync(orderId)
                ?? throw new InvalidOperationException("Stripe BNPL payment record was not found for refund completion.");
            if (record.RefundedAmountMinor >= cumulativeRefundedAmountMinor)
                return;
            if (record.PendingRefundAmountMinor != cumulativeRefundedAmountMinor)
                throw new InvalidOperationException("Stripe BNPL refund claim no longer belongs to this worker.");

            var now = DateTime.UtcNow;
            var targetStatus = SelectMonotonicLifecycleStatus(record.Status, status);
            var affected = await _repository.Table
                .Where(item => item.Id == record.Id && item.Status == record.Status &&
                               item.UpdatedOnUtc == record.UpdatedOnUtc &&
                               item.PendingRefundAmountMinor == cumulativeRefundedAmountMinor)
                .Set(item => item.RefundedAmountMinor,
                    Math.Max(record.RefundedAmountMinor, cumulativeRefundedAmountMinor))
                .Set(item => item.PendingRefundAmountMinor, (long?)null)
                .Set(item => item.RefundClaimedOnUtc, (DateTime?)null)
                .Set(item => item.Status, targetStatus)
                .Set(item => item.UpdatedOnUtc, now)
                .UpdateAsync();
            if (affected == 1)
                return;
        }

        throw new InvalidOperationException("Stripe BNPL refund completion conflicted with another lifecycle event.");
    }

    public async Task FailRefundAsync(int orderId, long cumulativeRefundedAmountMinor)
    {
        var record = await GetByOrderIdAsync(orderId);
        if (record?.PendingRefundAmountMinor != cumulativeRefundedAmountMinor)
            return;
        await _repository.Table
            .Where(item => item.Id == record.Id &&
                           item.PendingRefundAmountMinor == cumulativeRefundedAmountMinor)
            .Set(item => item.PendingRefundAmountMinor, (long?)null)
            .Set(item => item.RefundClaimedOnUtc, (DateTime?)null)
            .Set(item => item.UpdatedOnUtc, DateTime.UtcNow)
            .UpdateAsync();
    }

    public async Task UpdateStatusAsync(StripeBnplPaymentRecord record, string status)
    {
        ArgumentNullException.ThrowIfNull(record);
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (!CanTransition(record.Status, status))
                return;
            var now = DateTime.UtcNow;
            var affected = await _repository.Table
                .Where(item => item.Id == record.Id && item.Status == record.Status &&
                               item.UpdatedOnUtc == record.UpdatedOnUtc)
                .Set(item => item.Status, status)
                .Set(item => item.UpdatedOnUtc, now)
                .UpdateAsync();
            if (affected == 1)
                return;
            record = await _repository.GetByIdAsync(record.Id);
            if (record == null)
                return;
        }

        throw new InvalidOperationException("Stripe BNPL lifecycle update conflicted with another event.");
    }

    private async Task ApplySnapshotAsync(StripeBnplPaymentRecord record, StripeBnplPaymentSnapshot snapshot,
        string status)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var now = DateTime.UtcNow;
            var completeSnapshot = IsCompleteFeeSnapshot(snapshot);
            var preserveClaimedFee = !completeSnapshot &&
                                     (string.Equals(record.FeeDataStatus, StripeBnplFeeDataStatus.Complete,
                                          StringComparison.OrdinalIgnoreCase) ||
                                      string.Equals(record.FeeDataStatus, StripeBnplFeeDataStatus.Reconciling,
                                          StringComparison.OrdinalIgnoreCase));
            var targetStatus = SelectMonotonicLifecycleStatus(record.Status, status);
            var feeStatus = preserveClaimedFee
                ? record.FeeDataStatus
                : NormalizeFeeDataStatus(snapshot);
            var affected = await _repository.Table
                .Where(item => item.Id == record.Id && item.Status == record.Status &&
                               item.UpdatedOnUtc == record.UpdatedOnUtc)
                .Set(item => item.ProviderId, (int)snapshot.Provider)
                .Set(item => item.SessionId, snapshot.SessionId)
                .Set(item => item.PaymentIntentId, snapshot.PaymentIntentId)
                .Set(item => item.ChargeId, snapshot.ChargeId)
                .Set(item => item.BalanceTransactionId,
                    preserveClaimedFee ? record.BalanceTransactionId : completeSnapshot ? snapshot.BalanceTransactionId : null)
                .Set(item => item.PaymentMethodType, snapshot.PaymentMethodType)
                .Set(item => item.Status, targetStatus)
                .Set(item => item.AmountMinor, snapshot.AmountMinor)
                .Set(item => item.FeeMinor,
                    preserveClaimedFee ? record.FeeMinor : completeSnapshot ? snapshot.FeeMinor : null)
                .Set(item => item.NetMinor,
                    preserveClaimedFee ? record.NetMinor : completeSnapshot ? snapshot.NetMinor : null)
                .Set(item => item.Currency, snapshot.Currency)
                .Set(item => item.SettlementCurrency,
                    preserveClaimedFee ? record.SettlementCurrency : completeSnapshot ? snapshot.SettlementCurrency : null)
                .Set(item => item.ExchangeRate,
                    preserveClaimedFee ? record.ExchangeRate : completeSnapshot ? snapshot.ExchangeRate : null)
                .Set(item => item.FeeDetailsJson,
                    preserveClaimedFee ? record.FeeDetailsJson : completeSnapshot ? snapshot.FeeDetailsJson : null)
                .Set(item => item.FeeDataStatus, feeStatus)
                .Set(item => item.FeeDataError, preserveClaimedFee ? record.FeeDataError : (string)null)
                .Set(item => item.FeeCompletedOnUtc,
                    preserveClaimedFee ? record.FeeCompletedOnUtc : completeSnapshot ? record.FeeCompletedOnUtc ?? now : null)
                .Set(item => item.IsSandbox, snapshot.IsSandbox)
                .Set(item => item.UpdatedOnUtc, now)
                .UpdateAsync();
            if (affected == 1)
                return;

            record = await GetByOrderIdAsync(snapshot.OrderId)
                ?? throw new InvalidOperationException("Stripe BNPL payment record disappeared during finalization.");
        }

        throw new InvalidOperationException("Stripe BNPL finalization conflicted with another lifecycle event.");
    }

    private static StripeBnplPaymentRecord ToEntity(StripeBnplPaymentSnapshot snapshot, string status, DateTime now) =>
        new()
        {
            OrderId = snapshot.OrderId,
            Provider = snapshot.Provider,
            SessionId = snapshot.SessionId,
            PaymentIntentId = snapshot.PaymentIntentId,
            ChargeId = snapshot.ChargeId,
            BalanceTransactionId = snapshot.BalanceTransactionId,
            PaymentMethodType = snapshot.PaymentMethodType,
            Status = status,
            AmountMinor = snapshot.AmountMinor,
            FeeMinor = snapshot.FeeMinor,
            NetMinor = snapshot.NetMinor,
            RefundedAmountMinor = 0,
            Currency = snapshot.Currency,
            SettlementCurrency = snapshot.SettlementCurrency,
            ExchangeRate = snapshot.ExchangeRate,
            FeeDetailsJson = snapshot.FeeDetailsJson,
            FeeDataStatus = NormalizeFeeDataStatus(snapshot),
            FeeReconciliationAttemptCount = 0,
            FeeCompletedOnUtc = IsCompleteFeeSnapshot(snapshot) ? now : null,
            IsSandbox = snapshot.IsSandbox,
            CreatedOnUtc = now,
            UpdatedOnUtc = now
        };

    internal static string SelectMonotonicLifecycleStatus(string current, string next) =>
        string.IsNullOrWhiteSpace(current) || CanTransition(current, next) ? next : current;

    private static void ApplyFeeSnapshot(StripeBnplPaymentRecord record, StripeBnplPaymentSnapshot snapshot,
        DateTime now)
    {
        if (!IsCompleteFeeSnapshot(snapshot) &&
            (string.Equals(record.FeeDataStatus, StripeBnplFeeDataStatus.Complete,
                 StringComparison.OrdinalIgnoreCase) ||
             string.Equals(record.FeeDataStatus, StripeBnplFeeDataStatus.Reconciling,
                 StringComparison.OrdinalIgnoreCase)))
            return;

        if (IsCompleteFeeSnapshot(snapshot))
        {
            record.BalanceTransactionId = snapshot.BalanceTransactionId;
            record.FeeMinor = snapshot.FeeMinor;
            record.NetMinor = snapshot.NetMinor;
            record.SettlementCurrency = snapshot.SettlementCurrency;
            record.ExchangeRate = snapshot.ExchangeRate;
            record.FeeDetailsJson = snapshot.FeeDetailsJson;
            record.FeeDataStatus = StripeBnplFeeDataStatus.Complete;
            record.FeeDataError = null;
            record.FeeCompletedOnUtc ??= now;
            return;
        }

        record.BalanceTransactionId = null;
        record.FeeMinor = null;
        record.NetMinor = null;
        record.SettlementCurrency = null;
        record.ExchangeRate = null;
        record.FeeDetailsJson = null;
        record.FeeDataStatus = StripeBnplFeeDataStatus.Pending;
        record.FeeDataError = null;
        record.FeeCompletedOnUtc = null;
    }

    private static bool IsCompleteFeeSnapshot(StripeBnplPaymentSnapshot snapshot) =>
        string.Equals(snapshot.FeeDataStatus, StripeBnplFeeDataStatus.Complete,
            StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(snapshot.BalanceTransactionId) && snapshot.FeeMinor.HasValue &&
        snapshot.NetMinor.HasValue;

    private static string NormalizeFeeDataStatus(StripeBnplPaymentSnapshot snapshot) =>
        IsCompleteFeeSnapshot(snapshot) ? StripeBnplFeeDataStatus.Complete : StripeBnplFeeDataStatus.Pending;

    private static bool IsTerminalLifecycleStatus(string status) =>
        string.Equals(status, "refunded", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, "partially_refunded", StringComparison.OrdinalIgnoreCase) ||
        status?.StartsWith("disputed_", StringComparison.OrdinalIgnoreCase) == true ||
        status?.StartsWith("dispute_closed_", StringComparison.OrdinalIgnoreCase) == true;

    private static bool CanTransition(string current, string next)
    {
        if (string.IsNullOrWhiteSpace(next) || string.Equals(current, next, StringComparison.OrdinalIgnoreCase))
            return false;

        // A late payment/refund/dispute event must never move an already observed
        // lifecycle state backwards. Refunds may still advance into disputes.
        var currentRank = GetLifecycleRank(current);
        var nextRank = GetLifecycleRank(next);
        return nextRank >= currentRank;
    }

    private static int GetLifecycleRank(string status)
    {
        if (status?.StartsWith("dispute_closed_", StringComparison.OrdinalIgnoreCase) == true)
            return 50;
        if (status?.StartsWith("disputed_", StringComparison.OrdinalIgnoreCase) == true)
            return 40;
        if (string.Equals(status, "refunded", StringComparison.OrdinalIgnoreCase))
            return 30;
        if (string.Equals(status, "partially_refunded", StringComparison.OrdinalIgnoreCase))
            return 20;
        if (string.Equals(status, "paid", StringComparison.OrdinalIgnoreCase))
            return 10;
        return 0;
    }
}
