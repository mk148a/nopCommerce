using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Orders;

namespace Nop.Plugin.Misc.StripeBnplCore.Services;

public sealed class BnplCartContextService : IBnplCartContextService
{
    private readonly IAddressService _addressService;
    private readonly ICountryService _countryService;
    private readonly ICurrencyService _currencyService;
    private readonly ICustomerService _customerService;
    private readonly IOrderTotalCalculationService _orderTotalCalculationService;
    private readonly IWorkContext _workContext;

    public BnplCartContextService(
        IAddressService addressService,
        ICountryService countryService,
        ICurrencyService currencyService,
        ICustomerService customerService,
        IOrderTotalCalculationService orderTotalCalculationService,
        IWorkContext workContext)
    {
        _addressService = addressService;
        _countryService = countryService;
        _currencyService = currencyService;
        _customerService = customerService;
        _orderTotalCalculationService = orderTotalCalculationService;
        _workContext = workContext;
    }

    public async Task<BnplCartContext> GetAsync(IList<ShoppingCartItem> cart)
    {
        if (cart == null || cart.Count == 0)
            return null;

        var customer = await _customerService.GetCustomerByIdAsync(cart[0].CustomerId);
        if (customer?.BillingAddressId == null)
            return null;

        var address = await _addressService.GetAddressByIdAsync(customer.BillingAddressId.Value);
        var country = address?.CountryId is > 0
            ? await _countryService.GetCountryByIdAsync(address.CountryId.Value)
            : null;
        var currency = await _workContext.GetWorkingCurrencyAsync();
        var total = (await _orderTotalCalculationService.GetShoppingCartTotalAsync(cart,
            usePaymentMethodAdditionalFee: false)).shoppingCartTotal;
        if (country == null || currency == null || !total.HasValue)
            return null;

        var customerAmount = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(total.Value, currency);
        return new BnplCartContext(country.TwoLetterIsoCode?.ToUpperInvariant(),
            currency.CurrencyCode?.ToUpperInvariant(), customerAmount);
    }

    public async Task<BnplCartContext> GetAsync(Order order)
    {
        if (order == null || order.Deleted || order.BillingAddressId <= 0 || order.CurrencyRate <= 0)
            return null;
        var address = await _addressService.GetAddressByIdAsync(order.BillingAddressId);
        var country = address?.CountryId is > 0
            ? await _countryService.GetCountryByIdAsync(address.CountryId.Value)
            : null;
        var currencyCode = order.CustomerCurrencyCode?.Trim().ToUpperInvariant();
        if (country == null || currencyCode?.Length != 3)
            return null;
        return new BnplCartContext(country.TwoLetterIsoCode?.ToUpperInvariant(), currencyCode,
            _currencyService.ConvertCurrency(order.OrderTotal, order.CurrencyRate));
    }
}
