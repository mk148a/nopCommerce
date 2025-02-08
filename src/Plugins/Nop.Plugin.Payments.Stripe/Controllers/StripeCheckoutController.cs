using Microsoft.AspNetCore.Mvc;
using Nop.Services.Orders;
using Nop.Services.Payments;

using Nop.Core.Http.Extensions;

using Nop.Core;
using Nop.Web.Controllers;
using Nop.Services.Logging;

namespace Nop.Plugin.Payments.Stripe.Controllers
{
    [AutoValidateAntiforgeryToken]
    public class StripeCheckoutController : BasePublicController
    {
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IWorkContext _workContext;
        private readonly ILogger _logger;
        
        public StripeCheckoutController(
            IOrderProcessingService orderProcessingService,
            IWorkContext workContext,
            ILogger logger)
        {
            _orderProcessingService = orderProcessingService;
            _workContext = workContext;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> Handle3DSecure(string paymentIntentId)
        {
            try
            {
                // Session'dan ProcessPaymentRequest'i al
                
                
                var processPaymentRequest =await HttpContext.Session.GetAsync<ProcessPaymentRequest>("OrderPaymentInfo");
                if (processPaymentRequest == null)
                {
                    await _logger.ErrorAsync("Stripe 3D Secure error: Payment request not found in session");
                    return Json(new { success = false, error = "Payment request not found" });
                }

                // PaymentIntent'i güncelle
                processPaymentRequest.CustomValues["PaymentIntentId"] = paymentIntentId;
                
                var placeOrderResult = await _orderProcessingService.PlaceOrderAsync(processPaymentRequest);
                if (placeOrderResult.Success)
                {
                    // Session'dan ProcessPaymentRequest'i temizle
                    await HttpContext.Session.RemoveAsync("OrderPaymentInfo");
                    
                    return Json(new 
                    { 
                        success = true, 
                        redirectUrl = Url.RouteUrl("CheckoutCompleted", new { orderId = placeOrderResult.PlacedOrder.Id }) 
                    });
                }
                
                await _logger.ErrorAsync($"Stripe 3D Secure error: Order placement failed. Errors: {string.Join(", ", placeOrderResult.Errors)}");
                return Json(new { success = false, errors = placeOrderResult.Errors });
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync("Stripe 3D Secure error during order placement", ex);
                return Json(new { success = false, error = "An error occurred during payment processing" });
            }
        }
    }
} 