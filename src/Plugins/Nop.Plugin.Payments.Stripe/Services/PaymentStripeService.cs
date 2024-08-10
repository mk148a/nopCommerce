using System;
using System.Threading.Tasks;
using LinqToDB.Common;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Nop.Core;
using Nop.Plugin.Payments.Stripe.Models;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Directory;

namespace Nop.Plugin.Payments.Stripe.Services
{
    public class PaymentStripeService : IPaymentStripeService
    {
        #region Fields

        private readonly ICustomerService _customerService;
        private readonly IAddressService _addressService;
        private readonly ICountryService _countryService;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly IStateProvinceService _stateProvinceService;


        #endregion

        #region Ctor

        public PaymentStripeService(ICustomerService customerService,
            IGenericAttributeService genericAttributeService,
            IAddressService addressService,
            ICountryService countryService,IStateProvinceService stateProvinceService)
        {
            _customerService = customerService;
            _addressService = addressService;
            _countryService = countryService;
            _genericAttributeService = genericAttributeService;
            _stateProvinceService=stateProvinceService;
        }

        #endregion

        #region Methods

        public static string TodDateFormat(DateTime? value)
        {
            if (value.HasValue)
            {
                return Convert.ToDateTime(value).ToString("yyyy-MM-dd HH:mm:ss");
            }

            return string.Empty;
        }
        public virtual async Task<Buyer> GetBuyer(int customerId)
        {
            try
            {


                var customer = await _customerService.GetCustomerByIdAsync(customerId);


                var billingAddress = await _addressService.GetAddressByIdAsync(customer.BillingAddressId ?? 0);
                var shippingAddress = await _addressService.GetAddressByIdAsync(customer.ShippingAddressId ?? 0);

                if (billingAddress == null)
                    throw new NopException("Customer billing address not set!");

                var country = await _countryService.GetCountryByIdAsync(billingAddress.CountryId ?? 0);
                if (country == null)
                    throw new NopException("Billing address country not set!");


                if (billingAddress == null)
                    throw new NopException("Customer billing address  not set!");




                var billingState =
                    await _stateProvinceService.GetStateProvinceByIdAsync(billingAddress.StateProvinceId ?? 0);
                if (billingState == null)
                {
                    billingState = await _stateProvinceService.GetStateProvinceByIdAsync(shippingAddress.StateProvinceId ?? 0);
                    if (billingState == null)
                    {
                        //throw new NopException("Billing and shipping address state not set! Please check your address");
                    }


                }


                Address billingAddres = new Address();
                billingAddres.Name = billingAddress.FirstName;
                billingAddres.Surname = billingAddress.LastName;
                billingAddres.Email = billingAddress.Email;
                billingAddres.GsmNumber = billingAddress.PhoneNumber;
                billingAddres.Address1 = billingAddress.Address1;
                billingAddres.Address2 = billingAddress.Address2;
                billingAddres.City = billingAddress.City;
                billingAddres.ZipCode = billingAddress.ZipPostalCode;
                billingAddres.State = "Other";
                billingAddres.Id = billingAddress.Id;
                if (billingState != null)
                {
                    billingAddres.State = billingState.Abbreviation;

                }

                billingAddres.Country = country.TwoLetterIsoCode;

                var buyer = new Buyer
                {
                    Customer = customer,
                    Id = customer.CustomerGuid.ToString(),
                    shippinAddress = null,
                    billingAddress = billingAddres,
                    Ip = customer.LastIpAddress,
                    RegistrationDate = TodDateFormat(customer.CreatedOnUtc),
                    LastLoginDate = TodDateFormat(customer.LastLoginDateUtc)
                };

                if (shippingAddress != null)
                {
                    var shippingState =
                        await _stateProvinceService.GetStateProvinceByIdAsync(shippingAddress.StateProvinceId ?? 0);

                    if (shippingState == null)
                    {
                        //throw new NopException("Shipping address state not set!");

                    }
                    var shippingAddressCountry = await _countryService.GetCountryByIdAsync(shippingAddress.CountryId ?? 0);
                    if (shippingAddressCountry == null)
                        throw new NopException("Shipping address country not set!");




                    Address shippingAddres = new Address();
                    shippingAddres.Name = shippingAddress.FirstName;
                    shippingAddres.Surname = shippingAddress.LastName;
                    shippingAddres.Email = shippingAddress.Email;
                    shippingAddres.GsmNumber = shippingAddress.PhoneNumber;
                    shippingAddres.Address1 = shippingAddress.Address1;
                    shippingAddres.Address2 = shippingAddress.Address2;
                    shippingAddres.City = shippingAddress.City;
                    shippingAddres.ZipCode = shippingAddress.ZipPostalCode;
                    shippingAddres.Id = shippingAddress.Id;

                    shippingAddres.State = "Other";
                    if (shippingState != null)
                    {
                        shippingAddres.State = shippingState.Abbreviation;

                    }
                    shippingAddres.Country = shippingAddressCountry.TwoLetterIsoCode;


                }
                return buyer;
            }
            catch (Exception e)
            {
                throw new NopException(e.Message);
            }

        }



        #endregion
    }
}