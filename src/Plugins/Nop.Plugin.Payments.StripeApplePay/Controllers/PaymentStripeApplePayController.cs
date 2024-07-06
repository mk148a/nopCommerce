using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Services.Orders;
using Nop.Web.Framework.Controllers;
using Stripe;

namespace Nop.Plugin.Payments.StripeApplePay.Controllers
{
    public class PaymentStripeApplePayController : BasePaymentController
    {
        private readonly IOrderService _orderService;

        public PaymentStripeApplePayController(IOrderService orderService)
        {
            _orderService = orderService;
        }

        [HttpPost]
        public async Task<IActionResult> CreatePaymentIntent()
        {
            StripeConfiguration.ApiKey = "your-secret-key"; // Replace with your actual Stripe secret key

            var options = new PaymentIntentCreateOptions
            {
                Amount = 5000, // Total amount in cents
                Currency = "usd",
                PaymentMethodTypes = new List<string> { "card", "apple_pay" },
            };

            var service = new PaymentIntentService();
            var intent = await service.CreateAsync(options);

            return Json(new { clientSecret = intent.ClientSecret });
        }

        [HttpPost]
        public async Task<IActionResult> ConfirmPayment([FromBody] ConfirmPaymentRequest request)
        {
            var service = new PaymentIntentService();
            var intent = await service.ConfirmAsync(request.PaymentIntentId, new PaymentIntentConfirmOptions
            {
                PaymentMethod = request.PaymentMethodId
            });

            if (intent.Status == "succeeded")
            {
                return Json(new { success = true });
            }
            else
            {
                return Json(new { error = intent.LastPaymentError?.Message });
            }
        }
    }

    public class ConfirmPaymentRequest
    {
        public string PaymentMethodId { get; set; }
        public string PaymentIntentId { get; set; }
    }
}
