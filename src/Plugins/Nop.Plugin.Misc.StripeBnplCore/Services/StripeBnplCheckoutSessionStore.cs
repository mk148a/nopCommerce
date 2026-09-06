using LinqToDB;
using Nop.Data;
using Nop.Plugin.Misc.StripeBnplCore.Domain;
using Stripe.Checkout;

namespace Nop.Plugin.Misc.StripeBnplCore.Services;

public sealed class StripeBnplCheckoutSessionStore : IStripeBnplCheckoutSessionStore
{
    private const int ReservationRetryLimit = 12;
    private const int UpdateRetryLimit = 8;

    private readonly IRepository<StripeBnplCheckoutSession> _repository;

    public StripeBnplCheckoutSessionStore(IRepository<StripeBnplCheckoutSession> repository)
    {
        _repository = repository;
    }

    public async Task<StripeBnplCheckoutSession> GetBySessionIdAsync(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return null;
        return await _repository.Table.FirstOrDefaultAsync(item => item.SessionId == sessionId);
    }

    public async Task<StripeBnplCheckoutSession> GetByPaymentIntentIdAsync(string paymentIntentId)
    {
        if (string.IsNullOrWhiteSpace(paymentIntentId))
            return null;
        return await _repository.Table.FirstOrDefaultAsync(item => item.PaymentIntentId == paymentIntentId);
    }

    public Task<StripeBnplCheckoutSession> GetLatestAsync(int orderId, BnplProvider provider)
    {
        var query = _repository.Table
            .Where(item => item.OrderId == orderId && item.ProviderId == (int)provider)
            .OrderByDescending(item => item.AttemptNumber)
            .ThenByDescending(item => item.Id);
        return LinqToDB.AsyncExtensions.FirstOrDefaultAsync(query);
    }

    public Task<int> GetAttemptCountAsync(int orderId, BnplProvider provider) =>
        _repository.Table.CountAsync(item => item.OrderId == orderId && item.ProviderId == (int)provider);

    public async Task<StripeBnplCheckoutSession> ReserveAttemptAsync(int orderId, Guid orderGuid,
        BnplProvider provider, long amountMinor, string currency, bool isSandbox,
        StripeBnplEligibilitySnapshotEnvelope eligibilitySnapshot)
    {
        ValidateReservation(orderId, orderGuid, provider, amountMinor, currency, eligibilitySnapshot);

        for (var retry = 0; retry < ReservationRetryLimit; retry++)
        {
            var now = DateTime.UtcNow;
            var latest = await GetLatestAsync(orderId, provider);
            if (latest != null &&
                (string.Equals(latest.Status, "creating", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(latest.Status, "open", StringComparison.OrdinalIgnoreCase)) &&
                latest.ExpiresOnUtc > now)
            {
                if (ReservationMatches(latest, amountMinor, currency, isSandbox, eligibilitySnapshot))
                    return latest;
                if (string.Equals(latest.Status, "creating", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        "A different Stripe BNPL Checkout attempt is currently being created for this order.");
            }

            if (latest != null && string.Equals(latest.Status, "creating", StringComparison.OrdinalIgnoreCase))
            {
                await UpdateStatusAsync(latest, "create_expired");
                continue;
            }

            var attemptNumber = checked((latest?.AttemptNumber ?? 0) + 1);
            var idempotencyKey = $"nop-bnpl-session-{orderGuid:N}-{(int)provider}-a{attemptNumber}";
            var record = new StripeBnplCheckoutSession
            {
                OrderId = orderId,
                Provider = provider,
                SessionId = $"pending:{idempotencyKey}",
                AmountMinor = amountMinor,
                Currency = currency.Trim().ToLowerInvariant(),
                Status = "creating",
                AttemptNumber = attemptNumber,
                IdempotencyKey = idempotencyKey,
                EligibilitySnapshotJson = eligibilitySnapshot.Json,
                EligibilitySnapshotHash = eligibilitySnapshot.Sha256,
                ExpiresOnUtc = now.AddMinutes(35),
                IsSandbox = isSandbox,
                CreatedOnUtc = now,
                UpdatedOnUtc = now
            };

            try
            {
                await _repository.InsertAsync(record, publishEvent: false);
                return record;
            }
            catch
            {
                // The unique (order, provider, attempt) index is the allocator.
                // If another worker won, consume its reservation; otherwise this
                // was not a concurrency collision and the original error matters.
                var winner = await GetAttemptAsync(orderId, provider, attemptNumber);
                if (winner == null)
                    throw;
                if (ReservationMatches(winner, amountMinor, currency, isSandbox, eligibilitySnapshot) &&
                    (string.Equals(winner.Status, "creating", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(winner.Status, "open", StringComparison.OrdinalIgnoreCase)))
                    return winner;
            }
        }

        throw new InvalidOperationException("Stripe BNPL Checkout attempt reservation could not acquire a stable row.");
    }

    public async Task CompleteAttemptAsync(StripeBnplCheckoutSession attempt, Session session)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        ArgumentNullException.ThrowIfNull(session);
        var record = await _repository.GetByIdAsync(attempt.Id)
            ?? throw new InvalidOperationException("Stripe BNPL Checkout attempt reservation was not found.");
        if (!string.Equals(record.IdempotencyKey, attempt.IdempotencyKey, StringComparison.Ordinal) ||
            !string.Equals(record.EligibilitySnapshotHash, attempt.EligibilitySnapshotHash,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Stripe BNPL Checkout attempt ownership changed.");

        var now = DateTime.UtcNow;
        var nextStatus = string.IsNullOrWhiteSpace(session.Status) ? "open" : session.Status;
        var affected = await _repository.Table
            .Where(item => item.Id == record.Id && item.Status == "creating" &&
                           item.UpdatedOnUtc == record.UpdatedOnUtc &&
                           item.IdempotencyKey == record.IdempotencyKey &&
                           item.EligibilitySnapshotHash == record.EligibilitySnapshotHash)
            .Set(item => item.SessionId, session.Id)
            .Set(item => item.CheckoutUrl, session.Url)
            .Set(item => item.PaymentIntentId, session.PaymentIntentId)
            .Set(item => item.Status, nextStatus)
            .Set(item => item.ExpiresOnUtc, session.ExpiresAt)
            .Set(item => item.UpdatedOnUtc, now)
            .UpdateAsync();
        if (affected == 1)
            return;

        var current = await _repository.GetByIdAsync(attempt.Id);
        if (current != null && string.Equals(current.SessionId, session.Id, StringComparison.Ordinal) &&
            string.Equals(current.IdempotencyKey, attempt.IdempotencyKey, StringComparison.Ordinal) &&
            string.Equals(current.EligibilitySnapshotHash, attempt.EligibilitySnapshotHash,
                StringComparison.OrdinalIgnoreCase))
            return;

        throw new InvalidOperationException("Stripe BNPL Checkout attempt completion lost its compare-and-swap claim.");
    }

    public async Task UpsertAsync(int orderId, BnplProvider provider, Session session, long amountMinor,
        string currency, bool isSandbox, int attemptNumber, string idempotencyKey,
        StripeBnplEligibilitySnapshotEnvelope eligibilitySnapshot)
    {
        ArgumentNullException.ThrowIfNull(session);
        ValidateEnvelope(eligibilitySnapshot);
        var record = await GetBySessionIdAsync(session.Id);
        var now = DateTime.UtcNow;
        if (record == null)
        {
            try
            {
                await _repository.InsertAsync(new StripeBnplCheckoutSession
                {
                    OrderId = orderId,
                    Provider = provider,
                    SessionId = session.Id,
                    CheckoutUrl = session.Url,
                    PaymentIntentId = session.PaymentIntentId,
                    AmountMinor = amountMinor,
                    Currency = currency,
                    Status = session.Status ?? "open",
                    AttemptNumber = attemptNumber,
                    IdempotencyKey = idempotencyKey,
                    EligibilitySnapshotJson = eligibilitySnapshot.Json,
                    EligibilitySnapshotHash = eligibilitySnapshot.Sha256,
                    ExpiresOnUtc = session.ExpiresAt,
                    IsSandbox = isSandbox,
                    CreatedOnUtc = now,
                    UpdatedOnUtc = now
                }, publishEvent: false);
                return;
            }
            catch
            {
                record = await GetBySessionIdAsync(session.Id);
                if (record == null)
                    throw;
            }
        }

        if (record.OrderId != orderId || record.Provider != provider || record.AmountMinor != amountMinor ||
            !string.Equals(record.Currency, currency, StringComparison.OrdinalIgnoreCase) ||
            record.IsSandbox != isSandbox || record.AttemptNumber != attemptNumber ||
            !string.Equals(record.IdempotencyKey, idempotencyKey, StringComparison.Ordinal) ||
            !string.Equals(record.EligibilitySnapshotHash, eligibilitySnapshot.Sha256,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Stripe BNPL Checkout Session immutable identity mismatch.");

        await UpdateSessionDetailsAsync(record, session);
    }

    public async Task UpdateStatusAsync(StripeBnplCheckoutSession session, string status,
        string paymentIntentId = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.Id <= 0)
            throw new InvalidOperationException("A persisted Stripe BNPL Checkout Session is required.");

        for (var retry = 0; retry < UpdateRetryLimit; retry++)
        {
            var current = await _repository.GetByIdAsync(session.Id);
            if (current == null)
                throw new InvalidOperationException("Stripe BNPL Checkout Session was not found.");
            var nextStatus = string.IsNullOrWhiteSpace(status) ? current.Status : status.Trim().ToLowerInvariant();
            var selectedStatus = SelectMonotonicStatus(current.Status, nextStatus);
            var selectedPaymentIntentId = string.IsNullOrWhiteSpace(paymentIntentId)
                ? current.PaymentIntentId
                : paymentIntentId;
            if (string.Equals(selectedStatus, current.Status, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(selectedPaymentIntentId, current.PaymentIntentId, StringComparison.Ordinal))
            {
                CopyMutableState(current, session);
                return;
            }

            var now = DateTime.UtcNow;
            var affected = await _repository.Table
                .Where(item => item.Id == current.Id && item.Status == current.Status &&
                               item.UpdatedOnUtc == current.UpdatedOnUtc)
                .Set(item => item.Status, selectedStatus)
                .Set(item => item.PaymentIntentId, selectedPaymentIntentId)
                .Set(item => item.UpdatedOnUtc, now)
                .UpdateAsync();
            if (affected != 1)
                continue;

            current.Status = selectedStatus;
            current.PaymentIntentId = selectedPaymentIntentId;
            current.UpdatedOnUtc = now;
            CopyMutableState(current, session);
            return;
        }

        throw new InvalidOperationException("Stripe BNPL Checkout Session status update lost repeated CAS races.");
    }

    public static string SelectMonotonicStatus(string current, string next)
    {
        if (string.IsNullOrWhiteSpace(next) || string.Equals(current, next, StringComparison.OrdinalIgnoreCase))
            return current;
        if (string.Equals(current, "paid", StringComparison.OrdinalIgnoreCase))
            return current;
        if (string.Equals(next, "paid", StringComparison.OrdinalIgnoreCase))
            return "paid";
        return GetStatusRank(next) > GetStatusRank(current) ? next : current;
    }

    private async Task UpdateSessionDetailsAsync(StripeBnplCheckoutSession initial, Session session)
    {
        var current = initial;
        for (var retry = 0; retry < UpdateRetryLimit; retry++)
        {
            var nextStatus = SelectMonotonicStatus(current.Status, session.Status ?? current.Status);
            var now = DateTime.UtcNow;
            var affected = await _repository.Table
                .Where(item => item.Id == current.Id && item.Status == current.Status &&
                               item.UpdatedOnUtc == current.UpdatedOnUtc)
                .Set(item => item.CheckoutUrl, session.Url)
                .Set(item => item.PaymentIntentId,
                    string.IsNullOrWhiteSpace(session.PaymentIntentId) ? current.PaymentIntentId : session.PaymentIntentId)
                .Set(item => item.Status, nextStatus)
                .Set(item => item.ExpiresOnUtc, session.ExpiresAt)
                .Set(item => item.UpdatedOnUtc, now)
                .UpdateAsync();
            if (affected == 1)
                return;
            current = await _repository.GetByIdAsync(current.Id)
                ?? throw new InvalidOperationException("Stripe BNPL Checkout Session disappeared during update.");
        }

        throw new InvalidOperationException("Stripe BNPL Checkout Session update lost repeated CAS races.");
    }

    private Task<StripeBnplCheckoutSession> GetAttemptAsync(int orderId, BnplProvider provider, int attemptNumber) =>
        _repository.Table.FirstOrDefaultAsync(item => item.OrderId == orderId &&
            item.ProviderId == (int)provider && item.AttemptNumber == attemptNumber);

    private static bool ReservationMatches(StripeBnplCheckoutSession record, long amountMinor, string currency,
        bool isSandbox, StripeBnplEligibilitySnapshotEnvelope snapshot) =>
        record.AmountMinor == amountMinor && record.IsSandbox == isSandbox &&
        string.Equals(record.Currency, currency, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(record.EligibilitySnapshotHash, snapshot.Sha256, StringComparison.OrdinalIgnoreCase);

    private static void ValidateReservation(int orderId, Guid orderGuid, BnplProvider provider, long amountMinor,
        string currency, StripeBnplEligibilitySnapshotEnvelope snapshot)
    {
        if (orderId <= 0 || orderGuid == Guid.Empty || !Enum.IsDefined(provider) || amountMinor <= 0 ||
            string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Stripe BNPL Checkout reservation identity is invalid.");
        ValidateEnvelope(snapshot);
    }

    private static void ValidateEnvelope(StripeBnplEligibilitySnapshotEnvelope snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var computed = StripeBnplEligibilitySnapshotCodec.ComputeHash(snapshot.Json);
        if (!string.Equals(computed, snapshot.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Stripe BNPL eligibility snapshot hash is invalid.", nameof(snapshot));
    }

    private static int GetStatusRank(string status)
    {
        if (string.Equals(status, "paid", StringComparison.OrdinalIgnoreCase))
            return 100;
        if (status is not null &&
            (status.Equals("expired", StringComparison.OrdinalIgnoreCase) ||
             status.Equals("canceled", StringComparison.OrdinalIgnoreCase) ||
             status.Equals("payment_failed", StringComparison.OrdinalIgnoreCase) ||
             status.Equals("create_expired", StringComparison.OrdinalIgnoreCase)))
            return 50;
        if (string.Equals(status, "not_reusable", StringComparison.OrdinalIgnoreCase))
            return 40;
        if (string.Equals(status, "complete", StringComparison.OrdinalIgnoreCase))
            return 30;
        if (string.Equals(status, "open", StringComparison.OrdinalIgnoreCase))
            return 20;
        if (string.Equals(status, "creating", StringComparison.OrdinalIgnoreCase))
            return 10;
        return 0;
    }

    private static void CopyMutableState(StripeBnplCheckoutSession source, StripeBnplCheckoutSession target)
    {
        target.Status = source.Status;
        target.PaymentIntentId = source.PaymentIntentId;
        target.UpdatedOnUtc = source.UpdatedOnUtc;
    }
}
