using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Payments.StripeApplePay.Models;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;
using Stripe;

namespace Nop.Plugin.Payments.StripeApplePay.Controllers
{
   
    public class PaymentStripeApplePayController : BasePaymentController
    {
        private readonly IShoppingCartService _shoppingCartService;
        private readonly IOrderTotalCalculationService _orderTotalCalculationService;
        private readonly IWorkContext _workContext;
        private readonly ICustomerService _customerService;
        private readonly StripeApplePayPaymentSettings _stripePaymentSettings;
        private readonly IStoreContext _storeContext;
        private readonly ICurrencyService _currencyService;
        private readonly IPermissionService _permissionService;
        private readonly ISettingService _settingService;
        private readonly INotificationService _notificationService;
        private readonly ILocalizationService _localizationService;



        public PaymentStripeApplePayController(IShoppingCartService shoppingCartService,
            IOrderTotalCalculationService orderTotalCalculationService,
            IWorkContext workContext,
            ICustomerService customerService, StripeApplePayPaymentSettings stripePaymentSettings, IStoreContext storeContext, ICurrencyService currencyService, IPermissionService permissionService,
            ISettingService settingService ,INotificationService notificationService,ILocalizationService localizationService)
        {
            
            _stripePaymentSettings = stripePaymentSettings;
            _shoppingCartService = shoppingCartService;
            _orderTotalCalculationService = orderTotalCalculationService;
            _workContext = workContext;
            _customerService = customerService;
            _storeContext = storeContext;
            _currencyService= currencyService;
            _permissionService = permissionService;
            _settingService = settingService;
            _notificationService = notificationService;
            _localizationService = localizationService;
        }
        private RequestOptions GetStripeApiRequestOptions()
        {
            return new RequestOptions
            {
                ApiKey = _stripePaymentSettings.SecretKey,
                IdempotencyKey = Guid.NewGuid().ToString()
            };
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
        /// <returns>A task that represents the asynchronous operation</returns>
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




            /* We do not clear cache after each setting update.
             * This behavior can increase performance because cached settings will not be cleared 
             * and loaded from database after each update */

            await _settingService.SaveSettingOverridablePerStoreAsync(stripePaymentSettings, x => x.PublishableKey, model.PublishableKey_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(stripePaymentSettings, x => x.SecretKey, model.SecretKey_OverrideForStore, storeScope, false);


            //now clear settings cache
            await _settingService.ClearCacheAsync();

            _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

            return await Configure();
        }

        [HttpPost]
        public async Task<IActionResult> CreatePaymentIntent()
        {
            StripeConfiguration.ApiKey = _stripePaymentSettings.SecretKey;

            var customer = await _workContext.GetCurrentCustomerAsync();
            var store = await _storeContext.GetCurrentStoreAsync();
            var cart = await _shoppingCartService.GetShoppingCartAsync(customer, ShoppingCartType.ShoppingCart, store.Id);

            if (!cart.Any())
                return BadRequest("No products in cart.");

            var currency = await _workContext.GetWorkingCurrencyAsync();
            var shoppingCartTotal = await _orderTotalCalculationService.GetShoppingCartTotalAsync(cart, true);
            var shoppingCartUnitPriceWithDiscount = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(shoppingCartTotal.shoppingCartTotal.Value, currency);

            var options = new PaymentIntentCreateOptions
            {
                Amount = (long)(shoppingCartUnitPriceWithDiscount * 100), // Total amount in cents
                Currency = currency.CurrencyCode.ToLower(),
                PaymentMethodTypes = new List<string> {"card" },
            };

            var service = new PaymentIntentService();
            var intent = await service.CreateAsync(options, GetStripeApiRequestOptions());

            return Json(new { clientSecret = intent.ClientSecret });
        }

        [HttpPost]
        public async Task<IActionResult> ConfirmPayment([FromBody] ConfirmPaymentRequest request)
        {
            StripeConfiguration.ApiKey = _stripePaymentSettings.SecretKey;

            var service = new PaymentIntentService();
            var intent = await service.ConfirmAsync(request.PaymentIntentId, new PaymentIntentConfirmOptions
            {
                PaymentMethod = request.PaymentMethodId
            }, GetStripeApiRequestOptions());

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
