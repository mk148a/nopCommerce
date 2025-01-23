using System.Linq;
using System.Threading.Tasks;
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

        #endregion

        #region Ctor

        public PaymentStripeController(ILocalizationService localizationService,
            INotificationService notificationService,
            IPermissionService permissionService,
            ISettingService settingService,
            IStoreContext storeContext,
            IOrderService orderService,
            IWorkContext workContext)
        {
            _localizationService = localizationService;
            _notificationService = notificationService;
            _permissionService = permissionService;
            _settingService = settingService;
            _storeContext = storeContext;
            _orderService = orderService;
            _workContext = workContext;
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

        /// <returns>A task that represents the asynchronous operation</returns>
        public async Task<IActionResult> CancelOrder()
        {
            var order = (await _orderService.SearchOrdersAsync((await _storeContext.GetCurrentStoreAsync()).Id,
                customerId: (await _workContext.GetCurrentCustomerAsync()).Id, pageSize: 1)).FirstOrDefault();

            if (order != null)
                return RedirectToRoute("OrderDetails", new { orderId = order.Id });

            return RedirectToRoute("Homepage");
        }

        #endregion
    }
}