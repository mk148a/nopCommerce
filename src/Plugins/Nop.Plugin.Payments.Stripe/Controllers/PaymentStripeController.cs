using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.Payments.Stripe.Models;
using Nop.Plugin.Payments.Stripe.Services;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;
using Stripe;

namespace Nop.Plugin.Payments.Stripe.Controllers
{
    [AuthorizeAdmin]
    [Area(AreaNames.ADMIN)]
    [AutoValidateAntiforgeryToken]
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
        private readonly IStripeWebhookService _stripeWebhookService;

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
            IStripeWebhookService stripeWebhookService)
        {
            _localizationService = localizationService;
            _notificationService = notificationService;
            _permissionService = permissionService;
            _settingService = settingService;
            _storeContext = storeContext;
            _orderService = orderService;
            _workContext = workContext;
            _stripePaymentSettings= stripePaymentSettings;
            _stripeWebhookService = stripeWebhookService;
        }

        #endregion

        #region Methods

        /// <returns>A task that represents the asynchronous operation</returns>
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
                WebhookSecretConfigured = !string.IsNullOrWhiteSpace(stripePaymentSettings.WebhookSecret),
                WebhookEndpointId = stripePaymentSettings.WebhookEndpointId,
                WebhookEndpointUrl = stripePaymentSettings.WebhookEndpointUrl,
                ActiveStoreScopeConfiguration = storeScope
            };

            if (storeScope > 0)
            {
                model.PublishableKey_OverrideForStore = await _settingService.SettingExistsAsync(stripePaymentSettings, x => x.PublishableKey, storeScope);
                model.SecretKey_OverrideForStore = await _settingService.SettingExistsAsync(stripePaymentSettings, x => x.SecretKey, storeScope);

            }

            return View("~/Plugins/Nop.Plugin.Payments.Stripe/Views/Configure.cshtml", model);
        }

        [HttpPost]
        /// <returns>A task that represents the asynchronous operation</returns>
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

            // Do not bind the stored signing secret back into the page. An empty password
            // field means "leave the existing secret unchanged".
            if (!string.IsNullOrWhiteSpace(model.WebhookSecret))
                stripePaymentSettings.WebhookSecret = model.WebhookSecret;
            


    
            /* We do not clear cache after each setting update.
             * This behavior can increase performance because cached settings will not be cleared 
             * and loaded from database after each update */

            await _settingService.SaveSettingOverridablePerStoreAsync(stripePaymentSettings, x => x.PublishableKey, model.PublishableKey_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(stripePaymentSettings, x => x.SecretKey, model.SecretKey_OverrideForStore, storeScope, false);
            if (!string.IsNullOrWhiteSpace(model.WebhookSecret))
                await _settingService.SaveSettingAsync(stripePaymentSettings);
            

            //now clear settings cache
            await _settingService.ClearCacheAsync();

            _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

            return await Configure();
        }

        [HttpPost]
        public async Task<IActionResult> SyncWebhook()
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermission.Configuration.MANAGE_PAYMENT_METHODS))
                return AccessDeniedView();

            try
            {
                var result = await _stripeWebhookService.SyncEndpointAsync();
                if (result.SigningSecretRequired)
                {
                    _notificationService.WarningNotification(
                        "Stripe webhook endpoint is synchronized, but its signing secret is not stored. Copy the endpoint secret from Stripe Dashboard into the Webhook signing secret field.");
                }
                else
                {
                    _notificationService.SuccessNotification(
                        $"Stripe webhook synchronized ({result.EndpointId}); {result.MatchingEndpointCount} existing endpoint(s) matched this URL.");
                }
            }
            catch (Exception exception)
            {
                _notificationService.ErrorNotification($"Stripe webhook synchronization failed: {exception.Message}", false);
            }

            return RedirectToAction(nameof(Configure));
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

        #endregion
    }
}
