using Microsoft.AspNetCore.Mvc;
using Nop.Services.Configuration;
using Nop.Services.Security;
using Nop.Services.Stores;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Core;
using Nop.Core.Domain.Stores;
using Nop.Core.Domain.Security;
using Nop.Plugin.Payments.StripeApplePay.Models;
using Stripe;
using System.Threading.Tasks;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;
using Nop.Core.Domain.Orders;
using Nop.Web.Framework;
using System.Collections.Generic;
using System;
using System.Linq;
using Nop.Core.Domain.Payments;
using Nop.Services.Directory;

namespace Nop.Plugin.Payments.StripeApplePay.Controllers
{
    public class PaymentStripeApplePayController : BasePaymentController
    {
        private readonly StripeApplePayPaymentSettings _stripePaymentSettings;
        private readonly ISettingService _settingService;
        private readonly IStoreContext _storeContext;
        private readonly IPermissionService _permissionService;
        private readonly INotificationService _notificationService;
        private readonly ILocalizationService _localizationService;
        private readonly IShoppingCartService _shoppingCartService;
        private readonly IOrderTotalCalculationService _orderTotalCalculationService;
        private readonly IWorkContext _workContext;
        private readonly ICurrencyService _currencyService;
        private readonly IOrderService _orderService;

        public PaymentStripeApplePayController(
            StripeApplePayPaymentSettings stripePaymentSettings,
            ISettingService settingService,
            IStoreContext storeContext,
            IPermissionService permissionService,
            INotificationService notificationService,
            ILocalizationService localizationService,
            IShoppingCartService shoppingCartService,
            IOrderTotalCalculationService orderTotalCalculationService,
            IWorkContext workContext, ICurrencyService currencyService, IOrderService orderService)
        {
            _stripePaymentSettings = stripePaymentSettings;
            _settingService = settingService;
            _storeContext = storeContext;
            _permissionService = permissionService;
            _notificationService = notificationService;
            _localizationService = localizationService;
            _shoppingCartService = shoppingCartService;
            _orderTotalCalculationService = orderTotalCalculationService;
            _workContext = workContext;
            _currencyService = currencyService;
            _orderService = orderService;
        }

        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        [AutoValidateAntiforgeryToken]
        public async Task<IActionResult> Configure()
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            //load settings for a chosen store scope
            var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var stripePaymentSettings = await _settingService.LoadSettingAsync<StripeApplePayPaymentSettings>(storeScope);

            var model = new ConfigurationModel
            {
                PublishableKey = stripePaymentSettings.PublishableKey,
                SecretKey = stripePaymentSettings.SecretKey,
                ActiveStoreScopeConfiguration = storeScope
            };

            if (storeScope > 0)
            {
                model.PublishableKey_OverrideForStore = await _settingService.SettingExistsAsync(stripePaymentSettings, x => x.PublishableKey, storeScope);
                model.SecretKey_OverrideForStore = await _settingService.SettingExistsAsync(stripePaymentSettings, x => x.SecretKey, storeScope);
            }

            return View("~/Plugins/Nop.Plugin.Payments.StripeApplePay/Views/Configure.cshtml", model);
        }

        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        [AutoValidateAntiforgeryToken]
        [HttpPost]
        public async Task<IActionResult> Configure(ConfigurationModel model)
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            if (!ModelState.IsValid)
                return await Configure();

            //load settings for a chosen store scope
            var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var stripePaymentSettings = await _settingService.LoadSettingAsync<StripeApplePayPaymentSettings>(storeScope);

            //save settings
            stripePaymentSettings.PublishableKey = model.PublishableKey;
            stripePaymentSettings.SecretKey = model.SecretKey;

            await _settingService.SaveSettingOverridablePerStoreAsync(stripePaymentSettings, x => x.PublishableKey, model.PublishableKey_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(stripePaymentSettings, x => x.SecretKey, model.SecretKey_OverrideForStore, storeScope, false);

            await _settingService.ClearCacheAsync();

            _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

            return await Configure();
        }

        [HttpPost]
        public async Task<IActionResult> CreatePaymentIntent([FromBody] CreatePaymentIntentRequest request)
        {
            var paymentIntentService = new PaymentIntentService();

            var paymentIntentOptions = new PaymentIntentCreateOptions
            {
                Amount = (long)(request.OrderTotal * 100), // Order total amount in cents
                Currency = "usd",
                PaymentMethodTypes = new List<string> { "card" },
            };

            var paymentIntent = await paymentIntentService.CreateAsync(paymentIntentOptions, GetStripeApiRequestOptions());

            return Json(new { clientSecret = paymentIntent.ClientSecret, paymentIntentId = paymentIntent.Id });
        }
        public class CreatePaymentIntentRequest
        {
            public decimal OrderTotal { get; set; }
            public Guid OrderGuid { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> ConfirmPayment([FromBody] ConfirmPaymentRequest request)
        {
            var paymentIntentService = new PaymentIntentService();
            var paymentIntent = await paymentIntentService.ConfirmAsync(request.PaymentIntentId, new PaymentIntentConfirmOptions
            {
                PaymentMethod = request.PaymentMethodId
            }, GetStripeApiRequestOptions());

            if (paymentIntent.Status == "succeeded")
            {
                // Order işlem sonrası işlemlerini burada yapabilirsiniz
                var order = await _orderService.GetOrderByGuidAsync(request.OrderGuid);
                if (order != null)
                {
                    order.PaymentStatus = PaymentStatus.Paid;
                    order.OrderStatus = OrderStatus.Processing;
                    await _orderService.UpdateOrderAsync(order);
                }

                return Json(new { success = true });
            }
            else
            {
                return Json(new { success = false, error = paymentIntent.LastPaymentError?.Message ?? "Payment failed." });
            }
        }
        [HttpPost]
        public async Task<IActionResult> AuthorizePayment([FromBody] AuthorizePaymentRequest request)
        {
            var paymentIntentService = new PaymentIntentService();
            var paymentIntent = await paymentIntentService.CreateAsync(new PaymentIntentCreateOptions
            {
                Amount = request.Amount,
                Currency = request.Currency,
                PaymentMethod = request.PaymentMethodId,
                ConfirmationMethod = "manual",
                Confirm = true,
            });

            if (paymentIntent.Status == "requires_action")
            {
                return Json(new { success = true, id = paymentIntent.Id, clientSecret = paymentIntent.ClientSecret });
            }
            else if (paymentIntent.Status == "succeeded")
            {
                return Json(new { success = true, id = paymentIntent.Id });
            }
            else
            {
                return Json(new { success = false, error = paymentIntent.LastPaymentError?.Message ?? "Payment failed." });
            }
        }
        private RequestOptions GetStripeApiRequestOptions()
        {
            return new RequestOptions
            {
                ApiKey = _stripePaymentSettings.SecretKey,
                IdempotencyKey = Guid.NewGuid().ToString()
            };
        }
    }

    public class AuthorizePaymentRequest
    {
        public string PaymentMethodId { get; set; }
        public long? Amount { get; set; }
        public string Currency { get; set; }
    }

    public class ConfirmPaymentRequest
    {
        public string PaymentMethodId { get; set; }
        public string PaymentIntentId { get; set; }
        public Guid OrderGuid { get; set; }
    }
}
