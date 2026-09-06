using Nop.Core;
using Stripe;

namespace Nop.Plugin.Misc.StripeBnplCore.Services;

public sealed class StripeBnplPaymentClient : IStripeBnplPaymentClient
{
    private readonly StripeBnplSettings _settings;

    public StripeBnplPaymentClient(StripeBnplSettings settings)
    {
        _settings = settings;
    }

    public async Task<PaymentIntent> GetPaymentIntentAsync(string paymentIntentId)
    {
        if (string.IsNullOrWhiteSpace(paymentIntentId))
            throw new ArgumentException("PaymentIntent id is required.", nameof(paymentIntentId));

        var service = new PaymentIntentService(CreateClient());
        return await service.GetAsync(paymentIntentId, new PaymentIntentGetOptions
        {
            Expand = new List<string> { "payment_method", "latest_charge.balance_transaction" }
        });
    }

    public async Task<Refund> CreateRefundAsync(string paymentIntentId, long? amountMinor, string idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(paymentIntentId))
            throw new ArgumentException("PaymentIntent id is required.", nameof(paymentIntentId));
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("An idempotency key is required.", nameof(idempotencyKey));

        var service = new RefundService(CreateClient());
        return await service.CreateAsync(new RefundCreateOptions
        {
            PaymentIntent = paymentIntentId,
            Amount = amountMinor
        }, new RequestOptions { IdempotencyKey = idempotencyKey });
    }

    private StripeClient CreateClient()
    {
        var key = _settings.GetActiveRestrictedKey();
        if (string.IsNullOrWhiteSpace(key))
            throw new NopException("Stripe restricted API key is not configured.");
        return new StripeClient(key.Trim());
    }
}
