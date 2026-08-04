namespace Nop.Plugin.Payments.StripeApplePay.Services;

/// <summary>
/// A wallet PaymentIntent awaiting customer-side 3DS confirmation.
/// </summary>
internal sealed record StripePendingPayment(string PaymentIntentId);
