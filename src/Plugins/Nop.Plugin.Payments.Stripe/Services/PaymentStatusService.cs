using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Nop.Plugin.Payments.Stripe.Models;
using Nop.Services.Orders;
using Stripe;

namespace Nop.Plugin.Payments.Stripe.Services;
// Services/PaymentStatusService.cs
public class PaymentStatusService : IPaymentStatusService
{
    private readonly StripePaymentProcessor _paymentProcessor;
    private readonly IOrderService _orderService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PaymentStatusService(
        StripePaymentProcessor paymentProcessor,
        IOrderService orderService,
        IHttpContextAccessor httpContextAccessor)
    {
        _paymentProcessor = paymentProcessor;
        _orderService = orderService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task MonitorPaymentAsync(string paymentIntentId)
    {
        _httpContextAccessor.HttpContext.Session.SetString("StripePaymentIntent", paymentIntentId);
    }

    public async Task<PaymentStatusResult> GetStatusAsync(string paymentIntentId)
    {
        try
        {
            var service = new PaymentIntentService();
            var paymentIntent = await service.GetAsync(
                paymentIntentId,
                null,
                _paymentProcessor.GetStripeApiRequestOptions()
            );

            return new PaymentStatusResult
            {
                Status = paymentIntent.Status,
                OrderId = paymentIntent.Metadata.TryGetValue("order_id", out var orderId) ? orderId : null,
                Errors = paymentIntent.LastPaymentError?.Message != null
                    ? new List<string> { paymentIntent.LastPaymentError.Message }
                    : new List<string>()
            };
        }
        catch (StripeException ex)
        {
            return new PaymentStatusResult
            {
                Status = "error",
                Errors = new List<string> { ex.StripeError.Message }
            };
        }
    }
}
