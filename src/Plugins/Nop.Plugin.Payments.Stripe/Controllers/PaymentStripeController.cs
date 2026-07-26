using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.Payments.Stripe.Models;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;
using Stripe;
using System.IO;
using Microsoft.AspNetCore.Http;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Http.Extensions;
using Nop.Services.Logging;

namespace Nop.Plugin.Payments.Stripe.Controllers
{
    public class PaymentStripeController : BasePaymentController
    {
        #region Fields

        private readonly ILocalizationService _localizationService;
        private readonly INotificationService _notificationService;
        private readonly IPermissionService _permissionService;
        private readonly ISettingService _settingService;
        private readonly IStoreContext _storeContext;
        private readonly IOrderService _orderService;
        private readonly IWorkContext _workContext;
        private readonly StripePaymentSettings _stripePaymentSettings;
        private readonly ILogger _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        #endregion

        #region Ctor

        public PaymentStripeController(ILocalizationService localizationService,
            INotificationService notificationService,
            IPermissionService permissionService,
            ISettingService settingService,
            IStoreContext storeContext,
            IOrderService orderService,
            IWorkContext workContext,
            StripePaymentSettings stripePaymentSettings,
            ILogger logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _localizationService = localizationService;
            _notificationService = notificationService;
            _permissionService = permissionService;
            _settingService = settingService;
            _storeContext = storeContext;
            _orderService = orderService;
            _workContext = workContext;
            _stripePaymentSettings = stripePaymentSettings;
            _logger = logger;
            _httpContextAccessor= httpContextAccessor;
        }

        #endregion

        #region Methods

        /// <returns>A task that represents the asynchronous operation</returns>
        /// 
        [AuthorizeAdmin]
        [Area(AreaNames.ADMIN)]
        [AutoValidateAntiforgeryToken]
        public async Task<IActionResult> Configure()
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermission.Configuration.MANAGE_PAYMENT_METHODS))
                return AccessDeniedView();

            //load settings for a chosen store scope
            var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var stripePaymentSettings = await _settingService.LoadSettingAsync<StripePaymentSettings>(storeScope);

            var model = new ConfigurationModel
            {
                PublishableKey = stripePaymentSettings.PublishableKey,
                SecretKey = stripePaymentSettings.SecretKey,
                ActiveStoreScopeConfiguration = storeScope,
                WebhookSecret=stripePaymentSettings.WebhookSecret,
            };

            if (storeScope > 0)
            {
                model.PublishableKey_OverrideForStore = await _settingService.SettingExistsAsync(stripePaymentSettings, x => x.PublishableKey, storeScope);
                model.SecretKey_OverrideForStore =
                    await _settingService.SettingExistsAsync(stripePaymentSettings, x => x.SecretKey, storeScope);
                model.WebhookSecret_OverrideForStore = await _settingService.SettingExistsAsync(stripePaymentSettings, x => x.WebhookSecret, storeScope);

            }

            return View("~/Plugins/Nop.Plugin.Payments.Stripe/Views/Configure.cshtml", model);
        }

        [HttpPost]
        /// <returns>A task that represents the asynchronous operation</returns>
        [AuthorizeAdmin]
        [Area(AreaNames.ADMIN)]
        [AutoValidateAntiforgeryToken]
        public async Task<IActionResult> Configure(ConfigurationModel model)
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermission.Configuration.MANAGE_PAYMENT_METHODS))
                return AccessDeniedView();

            if (!ModelState.IsValid)
                return await Configure();

            //load settings for a chosen store scope
            var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var stripePaymentSettings = await _settingService.LoadSettingAsync<StripePaymentSettings>(storeScope);

            //save settings
            stripePaymentSettings.PublishableKey = model.PublishableKey;
            stripePaymentSettings.SecretKey = model.SecretKey;
            stripePaymentSettings.Enable3DS = model.Enable3DS;
            stripePaymentSettings.WebhookSecret = model.WebhookSecret;
            


    
            /* We do not clear cache after each setting update.
             * This behavior can increase performance because cached settings will not be cleared 
             * and loaded from database after each update */

            await _settingService.SaveSettingOverridablePerStoreAsync(stripePaymentSettings, x => x.PublishableKey, model.PublishableKey_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(stripePaymentSettings, x => x.SecretKey, model.SecretKey_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(stripePaymentSettings, x => x.Enable3DS, model.Enable3DS_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(stripePaymentSettings, x => x.WebhookSecret, model.WebhookSecret_OverrideForStore, storeScope, false);
            

            //now clear settings cache
            await _settingService.ClearCacheAsync();

            _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

            return await Configure();
        }

        /// <returns>A task that represents the asynchronous operation</returns>

       
        public async Task<IActionResult> CancelOrder()
        {
            var order = (await _orderService.SearchOrdersAsync((await _storeContext.GetCurrentStoreAsync()).Id,
                customerId: (await _workContext.GetCurrentCustomerAsync()).Id, pageSize: 1)).FirstOrDefault();

            if (order != null)
                return RedirectToRoute("OrderDetails", new { orderId = order.Id });

            return RedirectToRoute("Homepage");
        }
        /// <summary>
        /// Set up for a call to the Stripe API
        /// </summary>
        /// <returns></returns>
        private RequestOptions GetStripeApiRequestOptions()
        {
            return new RequestOptions
            {
                ApiKey = _stripePaymentSettings.SecretKey,
                IdempotencyKey = Guid.NewGuid().ToString()
            };
        }
        // Controllers/PaymentStripeController.cs
        [AllowAnonymous]
        public async Task<IActionResult> CheckPaymentStatus(string paymentIntentId)
        {
            try
            {
                var service = new PaymentIntentService();

                var paymentIntent = await service.GetAsync(paymentIntentId, null, GetStripeApiRequestOptions());

                return Json(new
                {
                    success = true,
                    status = paymentIntent.Status,
                    message =await GetStatusMessage(paymentIntent.Status)
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        private async Task<string> GetStatusMessage(string status)
        {
            return status switch
            {
                "requires_action" => await _localizationService.GetResourceAsync("Plugins.Payments.Stripe.StatusRequiresAction"),
                "requires_confirmation" => await _localizationService.GetResourceAsync("Plugins.Payments.Stripe.StatusRequiresConfirmation"),
                "processing" => await _localizationService.GetResourceAsync("Plugins.Payments.Stripe.StatusProcessing"),
                _ => await _localizationService.GetResourceAsync("Plugins.Payments.Stripe.StatusUnknown")
            };
        }

        [HttpPost]
        public async Task<IActionResult> Confirm3DSecure(string paymentIntentId)
        {
            try
            {
                var service = new PaymentIntentService();
                var paymentIntent = await service.GetAsync(paymentIntentId);
                
                if (paymentIntent.Status == "succeeded")
                {
                    var order = await ResolveOrderAsync(paymentIntent);
                    if (order != null)
                    {
                        order.PaymentStatus = PaymentStatus.Paid;
                        order.OrderStatus = OrderStatus.Processing;
                        await _orderService.UpdateOrderAsync(order);
                        
                        return Json(new { success = true });
                    }
                }
                
                return Json(new { success = false, error = "Payment could not be confirmed" });
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync("Stripe 3D Secure confirmation error", ex);
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> WebhookHandler()
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
            
            try
            {
             
                var stripeEvent = EventUtility.ConstructEvent(
                    json,
                    Request.Headers["Stripe-Signature"],
                    _stripePaymentSettings.WebhookSecret
                );
                
                switch (stripeEvent.Type)
                {
                    case "payment_intent.succeeded":
                        if (stripeEvent.Data.Object is PaymentIntent paymentIntent)
                            await HandleSuccessfulPayment(paymentIntent);
                        break;
                        
                    case "payment_intent.payment_failed":
                        if (stripeEvent.Data.Object is PaymentIntent failedPaymentIntent)
                            await HandleFailedPayment(failedPaymentIntent);
                        break;
                    case "charge.succeeded":
                        if (stripeEvent.Data.Object is Charge charge)
                            await HandleSuccessfulPayment(charge);
                        break;
                    case "charge.failed":
                        if (stripeEvent.Data.Object is Charge chargefail)
                            await HandleFailedPayment(chargefail);
                        break;
                }

                return Ok();
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync("Stripe webhook error", ex);
                return BadRequest();
            }
        }
        private async Task<Order> ResolveOrderAsync(IDictionary<string, string> metadata)
        {
            if (metadata == null)
                return null;

            if (metadata.TryGetValue("order_guid", out var orderGuidText) &&
                Guid.TryParse(orderGuidText, out var orderGuid))
            {
                var orderByGuid = await _orderService.GetOrderByGuidAsync(orderGuid);
                if (orderByGuid != null)
                    return orderByGuid;
            }

            if (metadata.TryGetValue("order_id", out var orderIdText) &&
                int.TryParse(orderIdText, out var orderId))
            {
                return await _orderService.GetOrderByIdAsync(orderId);
            }

            return null;
        }

        private Task<Order> ResolveOrderAsync(PaymentIntent paymentIntent)
        {
            return ResolveOrderAsync(paymentIntent?.Metadata);
        }

        private async Task<Order> ResolveOrderAsync(Charge charge)
        {
            var order = await ResolveOrderAsync(charge?.Metadata);
            if (order != null || string.IsNullOrWhiteSpace(charge?.PaymentIntentId))
                return order;

            // Charge metadata may be absent or stale. The PaymentIntent is the
            // authoritative object for this integration.
            var paymentIntent = await new PaymentIntentService().GetAsync(
                charge.PaymentIntentId,
                null,
                GetStripeApiRequestOptions());

            return await ResolveOrderAsync(paymentIntent);
        }

        private async Task HandleSuccessfulPayment(Charge charge)
        {
            var order = await ResolveOrderAsync(charge);
            if (order == null)
            {
                await _logger.InformationAsync($"Stripe charge event ignored because no nopCommerce order metadata was found. ChargeId={charge?.Id}, PaymentIntentId={charge?.PaymentIntentId}");
                return;
            }

            if (order.PaymentStatus == PaymentStatus.Paid)
                return;

            order.PaymentStatus = PaymentStatus.Paid;
            order.OrderStatus = OrderStatus.Processing;
            await _orderService.UpdateOrderAsync(order);
        }

        private async Task HandleSuccessfulPayment(PaymentIntent paymentIntent)
        {
            var order = await ResolveOrderAsync(paymentIntent);
            if (order == null)
            {
                await _logger.InformationAsync($"Stripe PaymentIntent event ignored because no nopCommerce order metadata was found. PaymentIntentId={paymentIntent?.Id}");
                return;
            }

            if (order.PaymentStatus == PaymentStatus.Paid)
                return;

            order.PaymentStatus = PaymentStatus.Paid;
            order.OrderStatus = OrderStatus.Processing;
            await _orderService.UpdateOrderAsync(order);
        }

        private async Task HandleFailedPayment(PaymentIntent paymentIntent)
        {
            var order = await ResolveOrderAsync(paymentIntent);
            if (order == null)
            {
                await _logger.InformationAsync($"Stripe failed PaymentIntent event ignored because no nopCommerce order metadata was found. PaymentIntentId={paymentIntent?.Id}");
                return;
            }

            // Never downgrade an order that has already been paid because Stripe
            // can deliver webhook events more than once and not always in order.
            if (order.PaymentStatus == PaymentStatus.Paid)
                return;

            order.PaymentStatus = PaymentStatus.Voided;
            order.OrderStatus = OrderStatus.Cancelled;
            await _orderService.UpdateOrderAsync(order);
        }

        private async Task HandleFailedPayment(Charge charge)
        {
            var order = await ResolveOrderAsync(charge);
            if (order == null)
            {
                await _logger.InformationAsync($"Stripe failed charge event ignored because no nopCommerce order metadata was found. ChargeId={charge?.Id}, PaymentIntentId={charge?.PaymentIntentId}");
                return;
            }

            if (order.PaymentStatus == PaymentStatus.Paid)
                return;

            order.PaymentStatus = PaymentStatus.Voided;
            order.OrderStatus = OrderStatus.Cancelled;
            await _orderService.UpdateOrderAsync(order);
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Check3DSecureStatus(string paymentIntentId)
        {
            try
            {
                var service = new PaymentIntentService();
                var paymentIntent = await service.GetAsync(paymentIntentId, null, GetStripeApiRequestOptions());

                if (paymentIntent.Status == "succeeded")
                {
                    var order = await ResolveOrderAsync(paymentIntent);
                    if (order != null)
                    {
                        order.PaymentStatus = PaymentStatus.Paid;
                        order.OrderStatus = OrderStatus.Processing;
                        await _orderService.UpdateOrderAsync(order);
                        return Json(new { success = true });
                    }
                }
                
                return Json(new { success = false, error = "Payment could not be completed" });
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync("Error checking 3D Secure status", ex);
                return Json(new { success = false, error = ex.Message });
            }
        }   
        
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> SaveError(string error)
        {
            try
            {
                 await _httpContextAccessor.HttpContext.Session.SetAsync("Stripe3DSError", error);

                 return Json(new { success = true });

               
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync("Error saving 3D Secure error message", ex);
                return Json(new { success = false, error = ex.Message });
            }
        }

        #endregion
    }
}