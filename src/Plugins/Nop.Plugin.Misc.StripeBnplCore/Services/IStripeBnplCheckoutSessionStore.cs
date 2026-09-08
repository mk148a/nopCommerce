using Nop.Plugin.Misc.StripeBnplCore.Domain;
using Stripe.Checkout;

namespace Nop.Plugin.Misc.StripeBnplCore.Services;

public interface IStripeBnplCheckoutSessionStore
{
    Task<StripeBnplCheckoutSession> GetBySessionIdAsync(string sessionId);
    Task<StripeBnplCheckoutSession> GetByPaymentIntentIdAsync(string paymentIntentId);
    Task<StripeBnplCheckoutSession> GetLatestAsync(int orderId, BnplProvider provider);
    Task<int> GetAttemptCountAsync(int orderId, BnplProvider provider);
    Task<StripeBnplCheckoutSession> ReserveAttemptAsync(int orderId, Guid orderGuid, BnplProvider provider,
        long amountMinor, string currency, bool isSandbox, StripeBnplEligibilitySnapshotEnvelope eligibilitySnapshot);
    Task CompleteAttemptAsync(StripeBnplCheckoutSession attempt, Session session);
    Task UpsertAsync(int orderId, BnplProvider provider, Session session, long amountMinor, string currency,
        bool isSandbox, int attemptNumber, string idempotencyKey,
        StripeBnplEligibilitySnapshotEnvelope eligibilitySnapshot);
    Task UpdateStatusAsync(StripeBnplCheckoutSession session, string status, string paymentIntentId = null);
}
