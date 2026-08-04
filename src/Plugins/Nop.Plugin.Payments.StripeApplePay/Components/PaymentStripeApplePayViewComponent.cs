using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Services.Configuration;
using Nop.Services.Orders;
using Nop.Services.Catalog;
using Nop.Web.Framework.Components;
using System.Threading.Tasks;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Payments.StripeApplePay.Models;
using System.Linq;
using System.Net;
using Nop.Core.Domain.Directory;
using Nop.Services.Common;
using Nop.Services.Directory;

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
        private readonly ICurrencyService _currencyService;
        private readonly IAddressService _addressService;
        private readonly ICountryService _countryService;

      public StripeApplePayViewComponent(IOrderService orderService, IStoreContext storeContext, IWorkContext workContext,
            ISettingService settingService, IOrderTotalCalculationService orderTotalCalculationService, IShoppingCartService shoppingCartService,
            ICurrencyService currencyService,IAddressService addressService, ICountryService countryService)
        {
            _orderService = orderService;
            _storeContext = storeContext;
            _workContext = workContext;
            _settingService = settingService;
            _orderTotalCalculationService = orderTotalCalculationService;
            _shoppingCartService = shoppingCartService;
            _currencyService = currencyService;
            _addressService = addressService;
            _countryService = countryService;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var currentCustomer = await _workContext.GetCurrentCustomerAsync();
            var currency = await _workContext.GetWorkingCurrencyAsync();
            var cart = await _shoppingCartService.GetShoppingCartAsync(currentCustomer, ShoppingCartType.ShoppingCart);
            var shoppingCartTotal = await _orderTotalCalculationService.GetShoppingCartTotalAsync(cart, true);
            var shoppingCartUnitPriceWithDiscount = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(shoppingCartTotal.shoppingCartTotal.Value, currency);
            var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var stripePaymentSettings = await _settingService.LoadSettingAsync<StripeApplePayPaymentSettings>(storeScope);

            var billingAddress = await _addressService.GetAddressByIdAsync(currentCustomer.BillingAddressId ?? 0);

            if (billingAddress == null)
                throw new NopException("Customer billing address not set!");

            var country = await _countryService.GetCountryByIdAsync(billingAddress.CountryId ?? 0);
            if (country == null)
                throw new NopException("Billing address country not set!");



            var billingAddressCountry = await _countryService.GetCountryByIdAsync(billingAddress.CountryId ?? 0);
            
          
                var model = new PaymentInfoModel
            {
                OrderTotal =shoppingCartUnitPriceWithDiscount*100,
                Currency = currency.CurrencyCode.ToLower(),
                Country = billingAddressCountry.TwoLetterIsoCode.ToUpper(),
                StripePublishableKey = stripePaymentSettings.PublishableKey
            };


            return View("~/Plugins/Nop.Plugin.Payments.StripeApplePay/Views/PaymentInfo.cshtml", model);
        }
    }
}
