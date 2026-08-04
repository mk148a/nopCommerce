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
using Nop.Core.Http.Extensions;
using Nop.Services.Directory;
using Nop.Services.Payments;
using Nop.Services.Common;

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
        private readonly IGenericAttributeService _genericAttributeService;

        public PaymentStripeApplePayController(
            StripeApplePayPaymentSettings stripePaymentSettings,
            ISettingService settingService,
            IStoreContext storeContext,
            IPermissionService permissionService,
            INotificationService notificationService,
            ILocalizationService localizationService,
            IShoppingCartService shoppingCartService,
            IOrderTotalCalculationService orderTotalCalculationService,
            IWorkContext workContext, ICurrencyService currencyService, IOrderService orderService, IGenericAttributeService genericAttributeService)
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
            _genericAttributeService= genericAttributeService;
        }

        [AuthorizeAdmin]
        [Area(AreaNames.ADMIN)]
        [AutoValidateAntiforgeryToken]
        public async Task<IActionResult> Configure()
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermission.Configuration.MANAGE_PAYMENT_METHODS))
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
        [Area(AreaNames.ADMIN)]
        [AutoValidateAntiforgeryToken]
        [HttpPost]
        public async Task<IActionResult> Configure(ConfigurationModel model)
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermission.Configuration.MANAGE_PAYMENT_METHODS))
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
            if (request == null)
            {
                return BadRequest("Invalid request payload");
            }

            var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var stripePaymentSettings = await _settingService.LoadSettingAsync<StripeApplePayPaymentSettings>(storeScope);

            StripeConfiguration.ApiKey = stripePaymentSettings.SecretKey;

            var paymentIntentService = new PaymentIntentService();

            var paymentIntentOptions = new PaymentIntentCreateOptions
            {
                Amount = (long)(request.OrderTotal ), // Order total amount in cents
                Currency = request.Currency,
                PaymentMethodTypes = new List<string> { "card" },
            };

            var paymentIntent = await paymentIntentService.CreateAsync(paymentIntentOptions, GetStripeApiRequestOptions());

            return Json(new { success = true, id = paymentIntent.Id, clientSecret = paymentIntent.ClientSecret });
        }


        [HttpPost]
        public async Task<IActionResult> CancelPaymentIntent(string paymentIntentId)
        {
            try
            {
                if (paymentIntentId==null)
                {
                    return Json(new { success = false, error = "paymentIntentId is null" });
                }
                var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
                var stripePaymentSettings = await _settingService.LoadSettingAsync<StripeApplePayPaymentSettings>(storeScope);

                StripeConfiguration.ApiKey = stripePaymentSettings.SecretKey;

                var paymentIntentService = new PaymentIntentService();
                var paymentIntent = await paymentIntentService.CancelAsync(paymentIntentId,null, GetStripeApiRequestOptions());

                return Json(new { success = true, paymentIntentId = paymentIntent.Id });
            }
            catch (StripeException e)
            {
                return Json(new { success = false, error = e.Message });
            }
        }
        public class CreatePaymentIntentRequest
        {
            public decimal OrderTotal { get; set; }
            public string Currency { get; set; }
        }

        //this method allows pass the paymentIntentId and PaymentMetodId to my website processpayment method
        [HttpPost]
        public async Task<IActionResult> ConfirmPayment([FromBody] ConfirmPaymentRequest request)
        {
            try
            {

          
            var paymentIntentId = request.PaymentIntentId;
            var paymentMethodId = request.PaymentMethodId;


            await _genericAttributeService.SaveAttributeAsync<string>(await _workContext.GetCurrentCustomerAsync(),
                "PaymentIntentId", paymentIntentId);

            await _genericAttributeService.SaveAttributeAsync<string>(await _workContext.GetCurrentCustomerAsync(),
                "PaymentMethodId", paymentMethodId);

          


                return Json(new { success = true });
            }
            catch (Exception e)
            {
                return Json(new { success = false,message=e.Message });
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
      
    }
}
