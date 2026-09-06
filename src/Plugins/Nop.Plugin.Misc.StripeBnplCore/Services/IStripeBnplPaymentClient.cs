using Stripe;

namespace Nop.Plugin.Misc.StripeBnplCore.Services;

public interface IStripeBnplPaymentClient
{
    Task<PaymentIntent> GetPaymentIntentAsync(string paymentIntentId);
    Task<Refund> CreateRefundAsync(string paymentIntentId, long? amountMinor, string idempotencyKey);
}
