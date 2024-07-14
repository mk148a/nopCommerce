using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Services.Configuration;
using Nop.Services.Orders;
using Nop.Services.Catalog;
using Nop.Web.Framework.Components;
using System.Threading.Tasks;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Payments.StripeApplePay.Models;

namespace Nop.Plugin.Payments.StripeApplePay.Components
{
    [ViewComponent(Name = "StripeApplePay")]
    public class StripeApplePayViewComponent : NopViewComponent
    {
        private readonly IOrderService _orderService;
        private readonly IStoreContext _storeContext;
        private readonly IWorkContext _workContext;
        private readonly ISettingService _settingService;
        private readonly IOrderTotalCalculationService _orderTotalCalculationService;
        private readonly IShoppingCartService _shoppingCartService;

        public StripeApplePayViewComponent(IOrderService orderService, IStoreContext storeContext, IWorkContext workContext, ISettingService settingService, IOrderTotalCalculationService orderTotalCalculationService, IShoppingCartService shoppingCartService)
        {
            _orderService = orderService;
            _storeContext = storeContext;
            _workContext = workContext;
            _settingService = settingService;
            _orderTotalCalculationService = orderTotalCalculationService;
            _shoppingCartService = shoppingCartService;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var currentCustomer = await _workContext.GetCurrentCustomerAsync();
            var cart = await _shoppingCartService.GetShoppingCartAsync(currentCustomer, ShoppingCartType.ShoppingCart);
            var orderTotal = (await _orderTotalCalculationService.GetShoppingCartTotalAsync(cart, true)).shoppingCartTotal;

            var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var stripePaymentSettings = await _settingService.LoadSettingAsync<StripeApplePayPaymentSettings>(storeScope);

            var model = new PaymentInfoModel
            {
                OrderTotal = orderTotal ?? 0,
                StripePublishableKey = stripePaymentSettings.PublishableKey
            };

            return View("~/Plugins/Nop.Plugin.Payments.StripeApplePay/Views/PaymentInfo.cshtml", model);
        }
    }
}
