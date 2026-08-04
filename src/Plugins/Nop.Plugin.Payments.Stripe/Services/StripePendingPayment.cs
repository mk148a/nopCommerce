namespace Nop.Plugin.Payments.Stripe.Services;

/// <summary>
/// A PaymentIntent that needs customer-side 3DS confirmation before nopCommerce
/// is allowed to create an order.  Only the Stripe id is retained; the client
/// secret is read from Stripe again when the checkout is retried.
/// </summary>
internal sealed record StripePendingPayment(string PaymentIntentId);
