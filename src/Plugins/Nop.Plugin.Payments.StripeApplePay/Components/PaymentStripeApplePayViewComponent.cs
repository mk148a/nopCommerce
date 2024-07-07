using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.Payments.StripeApplePay.Models;
using Nop.Services.Configuration;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Payments.StripeApplePay.Components
{
    [ViewComponent(Name = StripeApplePayPaymentDefaults.ViewComponentName)]
    public class StripeApplePayViewComponent : NopViewComponent
    {
            private readonly ISettingService _settingService;
            private readonly IStoreContext _storeContext;

            public StripeApplePayViewComponent(ISettingService settingService, IStoreContext storeContext)
            {
                _settingService = settingService;
                _storeContext = storeContext;
            }

            public async Task<IViewComponentResult> InvokeAsync()
            {
                var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
                var stripePaymentSettings = await _settingService.LoadSettingAsync<StripeApplePayPaymentSettings>(storeScope);

                var model = new PaymentInfoModel
                {
                    StripePublishableKey = stripePaymentSettings.PublishableKey,
                    OrderTotal = 0 // Order total will be dynamically set
                };

                return View("~/Plugins/Nop.Plugin.Payments.StripeApplePay/Views/PaymentInfo.cshtml", model);
            }
        

    }
}
