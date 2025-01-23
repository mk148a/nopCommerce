using Nop.Plugin.Payments.Stripe.Models;

namespace Nop.Plugin.Payments.Stripe.Services;

public interface IPaymentStatusService
{
    Task MonitorPaymentAsync(string paymentIntentId);
    Task<PaymentStatusResult> GetStatusAsync(string paymentIntentId);
}

