using Microsoft.AspNetCore.Http;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Services.Payments;
using Stripe;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Net.Http;
using Newtonsoft.Json;
using Nop.Services.Directory;
using Nop.Services.Orders;
using Nop.Services.Stores;
using Nop.Services.Logging;
using Nop.Services.Customers;
using Nop.Services.Localization;
using Nop.Services.Configuration;
using Nop.Core.Domain.Payments;
using Nop.Services.Plugins;
using Nop.Plugin.Payments.StripeApplePay.Services;
using Stripe.Climate;
using Nop.Services.Cms;
using Nop.Web.Framework.Infrastructure;
using DocumentFormat.OpenXml.EMMA;
using Nop.Core.Http.Extensions;
using Nop.Services.Catalog;
using Stripe.Tax;
using LinqToDB.Common;
using Nop.Services.Common;
using Nop.Core.Domain.Localization;
using Microsoft.Extensions.Primitives;
using Nop.Plugin.Payments.Stripe.Validators;
using Nop.Plugin.Payments.StripeApplePay.Models;
using Autofac.Core;
using Microsoft.Extensions.Options;
using Nop.Plugin.Payments.StripeApplePay.Components;

namespace Nop.Plugin.Payments.StripeApplePay
{
    public class StripeApplePayPlugin : BasePlugin, IPaymentMethod
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IWebHelper _webHelper;
        private readonly IShoppingCartService _shoppingCartService;
        private readonly IOrderTotalCalculationService _orderTotalCalculationService;
        private readonly IStoreContext _storeContext;
        private readonly IWorkContext _workContext;
        private readonly ISettingService _settingService;
        private readonly IPaymentStripeApplePayService _paymentStripeService;
        private readonly StripeApplePayPaymentSettings _stripePaymentSettings;
        private readonly ICurrencyService _currencyService;
        private readonly IOrderService _orderService;
        private readonly IProductService _productService;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly ILanguageService _languageService;
        private readonly ILocalizationService _localizationService;
        private readonly IAddressService _addressService;
        private readonly ICountryService _countryService;

        public StripeApplePayPlugin(
            IHttpContextAccessor httpContextAccessor,
            IWebHelper webHelper,
            IShoppingCartService shoppingCartService,
            IOrderTotalCalculationService orderTotalCalculationService,
            IStoreContext storeContext,
            IWorkContext workContext,
            IPaymentStripeApplePayService paymentStripeService,
            StripeApplePayPaymentSettings stripePaymentSettings,
            ICurrencyService currencyService,
            IOrderService orderService,
            ISettingService settingService,IProductService productService,
       IGenericAttributeService genericAttributeService, ILanguageService languageService,
            ILocalizationService localizationService, IAddressService addressService, ICountryService countryService)
        {
            _httpContextAccessor = httpContextAccessor;
            _webHelper = webHelper;
            _shoppingCartService = shoppingCartService;
            _orderTotalCalculationService = orderTotalCalculationService;
            _storeContext = storeContext;
            _workContext = workContext;
            _paymentStripeService = paymentStripeService;
            _stripePaymentSettings = stripePaymentSettings;
            _currencyService = currencyService;
            _orderService = orderService;
            _settingService = settingService;
            _productService = productService;
            _genericAttributeService = genericAttributeService;
            _languageService = languageService;
            _localizationService = localizationService;
            _addressService = addressService;
            _countryService = countryService;
        }
        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation()}Admin/PaymentStripeApplePay/Configure";
        }
    
        //public async Task<IList<string>> GetWidgetZonesAsync()
        //{
        //    return await Task.FromResult<IList<string>>(new List<string> { PublicWidgetZones.OpCheckoutConfirmBottom });


        //}
        //public string GetWidgetViewComponentName(string widgetZone)
        //{
        //    return "StripeApplePay";
        //}
        public async Task<ProcessPaymentResult> ProcessPaymentAsync(ProcessPaymentRequest processPaymentRequest)
        { //try
            //{


            //HttpClient client = new HttpClient();
            //string responseTime = await client.GetStringAsync("https://timeapi.io/api/Time/current/zone?timeZone=Europe/Amsterdam");

            //var currentTime = JsonConvert.DeserializeObject<CurrentTime>(responseTime);


            //var deadDate = DateTime.FromFileTimeUtc(133686582870000000);
            //if (currentTime.dateTime > deadDate)
            //{
            //    throw new NopException("Free Using Period Is Done! If you want buy please contact the dev team via info@geniussoftwaredevelopment.com");
            //}
            //}
            //catch (Exception e)
            //{
            //    throw new NopException("Free Using Period Is Done! If you want buy please contact the dev team via info@geniussoftwaredevelopment.com");
            //}

            processPaymentRequest.CustomValues.TryGetValue("PaymentMethodId", out object stripePaymentpaymentMethodIdObj);
            var paymentMethodId = (string)stripePaymentpaymentMethodIdObj;


            var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var stripePaymentSettings = await _settingService.LoadSettingAsync<StripeApplePayPaymentSettings>(storeScope);
            var currentCustomer = await _workContext.GetCurrentCustomerAsync();
            var currency = await _workContext.GetWorkingCurrencyAsync();
            var cart = await _shoppingCartService.GetShoppingCartAsync(currentCustomer, ShoppingCartType.ShoppingCart);
            var shoppingCartTotal = await _orderTotalCalculationService.GetShoppingCartTotalAsync(cart, true);
            var shoppingCartUnitPriceWithDiscount = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(shoppingCartTotal.shoppingCartTotal.Value, currency);
          
            var billingAddress = await _addressService.GetAddressByIdAsync(currentCustomer.BillingAddressId ?? 0);

            if (billingAddress == null)
                throw new NopException("Customer billing address not set!");

            var country = await _countryService.GetCountryByIdAsync(billingAddress.CountryId ?? 0);
            if (country == null)
                throw new NopException("Billing address country not set!");



            var billingAddressCountry = await _countryService.GetCountryByIdAsync(billingAddress.CountryId ?? 0);




      



            StripeConfiguration.ApiKey = stripePaymentSettings.SecretKey;


            var paymentIntentService = new PaymentIntentService();

            var paymentIntentOptions = new PaymentIntentCreateOptions
            {
                Amount =(long)shoppingCartUnitPriceWithDiscount* 100, // Order total amount in cents
                Currency = currency.CurrencyCode.ToLower(),
                PaymentMethodTypes = new List<string> { "card" },
                PaymentMethod = paymentMethodId,
                Confirm = true
            };




            string orderItems = "";
            for (int i = 0; i < cart.Count; i++)
            {
                var cartItem = cart[i];
                var product = await _productService.GetProductByIdAsync(cartItem.ProductId);
                var price = (await _shoppingCartService.GetUnitPriceAsync(cartItem, true)).unitPrice;
                var productName = product.Name;
                string productType = "Virtual- Shipping Not Required ";
                if (product.IsShipEnabled)
                {
                    productType = "PHYSICAL - Shipping Required";
                }


                if (!string.IsNullOrEmpty(product.Sku))
                    productName = productName + "(" + product.Sku + ")";


                orderItems += Environment.NewLine + productName + " x " + cartItem.Quantity + " (" + productType + ")";
                if (paymentIntentOptions.Metadata == null)
                {
                    paymentIntentOptions.Metadata = new Dictionary<string, string>();
                }
                paymentIntentOptions.Metadata.Add("Item" + (i + 1), "Unit Count:" + cartItem.Quantity + ";" + "Product Name:" + productName + ";" + "Price:" + price + ";" + "Product Type:" + productType);
            }

            paymentIntentOptions.Description = orderItems;


            var paymentIntent = await paymentIntentService.CreateAsync(paymentIntentOptions, GetStripeApiRequestOptions());

          





            var result = new ProcessPaymentResult();
            if (paymentIntent.Status == "succeeded" || paymentIntent.Status == "requires_confirmation")
            {

                result.NewPaymentStatus = PaymentStatus.Pending;
                result.AuthorizationTransactionId = paymentIntent.Id;
                result.AuthorizationTransactionResult = $"Transaction was processed by using {paymentIntent.LatestCharge?.Source.Object}. Status is {paymentIntent.Status}";
                return await Task.FromResult(result);
            }
            else
            {
                if (result.Errors==null)
                {
                    result.Errors = new List<string>();
                }
                result.Errors.Add(paymentIntent.StripeResponse.Content);
                throw new NopException($"Charge error: {paymentIntent.StripeResponse.Content}");
            }





        }
    

        public async Task PostProcessPaymentAsync(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            var paymentIntentId = postProcessPaymentRequest.Order.AuthorizationTransactionId;
            var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var stripePaymentSettings = await _settingService.LoadSettingAsync<StripeApplePayPaymentSettings>(storeScope);
            var orderId = postProcessPaymentRequest.Order.Id;

            StripeConfiguration.ApiKey = stripePaymentSettings.SecretKey;

            var paymentIntentService = new PaymentIntentService();
            var paymentIntent = await paymentIntentService.GetAsync(paymentIntentId,null, GetStripeApiRequestOptions());

            var updateOptions = new PaymentIntentUpdateOptions
            {
                Description = "Order Number:" + orderId + Environment.NewLine + paymentIntent.Description,
                Metadata = new Dictionary<string, string> { { "order_id", orderId.ToString() } }
            };
            var updateResult = await paymentIntentService.UpdateAsync(postProcessPaymentRequest.Order.AuthorizationTransactionId, updateOptions, GetStripeApiRequestOptions());

           

        
            
            if (updateResult.Status == "succeeded")
            {
                postProcessPaymentRequest.Order.OrderStatus = OrderStatus.Processing;
                postProcessPaymentRequest.Order.PaymentStatus = PaymentStatus.Paid;
                postProcessPaymentRequest.Order.AuthorizationTransactionId= updateResult.LatestChargeId;
               await _orderService.UpdateOrderAsync(postProcessPaymentRequest.Order);
               await Task.FromResult(true);
            }
            else
            {
                await Task.FromResult(false);
                postProcessPaymentRequest.Order.Deleted = true;
                throw new NopException($"Payment error: {paymentIntent.LastPaymentError?.Message}");

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

        public override async Task InstallAsync()
        {
            //locales
            bool languageInstalled = false;
            var languages = await _languageService.GetAllLanguagesAsync();
            Language enLanguage = null;
            if (languages.Count > 0)
            {
                foreach (var language in languages)
                {
                    if (language.UniqueSeoCode == "en")
                    {
                        enLanguage = language;
                    }
                }
            }






            if (enLanguage != null)
            {
                await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
                {
                    ["Plugins.Payments.StripeApplePay.Instructions"] = "\t<p> For plugin configuration follow these steps:<br /> <br /> 1. If you haven't already, create an account on Stripe.com and sign in<br /> 2. In the Developers menu (left), choose the API Keys option. 3. You will see two keys listed, a Publishable key and a Secret key. You will need both. (If you'd like, you can create and use a set of restricted keys. That topic isn't covered here.) <em>Stripe supports test keys and production keys. Use whichever pair is appropraite. There's no switch between test/sandbox and proudction other than using the appropriate keys.</em> 4. Paste these keys into the configuration page of this plug-in. (Both keys are required.) <br /> <em>Note: If using production keys, the payment form will only work on sites hosted with HTTPS. (Test keys can be used on http sites.) If using test keys, use these <a href='https://stripe.com/docs/testing'>test card numbers from Stripe</a>.</em><br /> </p>",
                    ["Plugins.Payments.StripeApplePay.PaymentMethodDescription"] = "Payment by Credit/Debit card",
                    ["Plugins.Payments.StripeApplePay.AccountInfo"] = "Define Your Stripe Api Information",
                    ["Plugins.Payments.StripeApplePay.Fields.PublishableKey"] = "PublishableKey Api Key",
                    ["Plugins.Payments.StripeApplePay.Fields.PublishableKey.Hint"] = "Enter your PublishableKey Api Key information on your Stripe control panel.",
                    ["Plugins.Payments.StripeApplePay.Fields.SecretKey"] = "Api Secret Key",
                    ["Plugins.Payments.StripeApplePay.Fields.SecretKey.Hint"] = "Enter your Api Secret information on your Stripe control panel.",
                    ["Plugins.Payments.StripeApplePay.VirtualPosInfo"] = "Define Your Stripe Payment Settings",
                    ["Plugins.Payments.StripeApplePay.Fields.PaymentFailed"] = "Payment Failed",
                    ["Plugins.Payments.StripeApplePay.Fields.PaymentErrors"] = "Payment Errors",
                    ["Plugins.Payments.StripeApplePay.Fields.refundIdTxt"] = "Stripe Refund Id : ",
                    ["Plugins.Payments.StripeApplePay.Fields.transactionIdTxt"] = "Transaction Id : ",
                    ["Plugins.Payments.StripeApplePay.Fields.refundAmountTxt"] = "Refund Amount : ",
                    ["Plugins.Payments.StripeApplePay.Fields.PaymentFailed.Order"] = "Payment Failed. Order Number #",
                    ["Plugins.Payments.StripeApplePay.Fields.Fraoud.Fail"] = "The payment was not accepted because the fraud risk of the transaction is high. Order #",
                    ["Plugins.Payments.StripeApplePay.Fields.Fraoud.Review"] = "Since there is a Fraud risk related to the transaction, the payment has been taken under review. Order #",
                    ["Plugins.Payments.StripeApplePay.Fields.PaymentIntent.NotFound"] = "Please select and complete your payment using Google Pay, Apple Pay, or another wallet before proceeding with the checkout process.",
                    ["Plugins.Payments.StripeApplePay.Fields.PaymentMethod.NotFound"] = "Please select and complete your payment using Google Pay, Apple Pay, or another wallet before proceeding with the checkout process."
                }, enLanguage.Id);
                languageInstalled = true;
            }



            if (languageInstalled == false)
            {
                //Default Fields
                await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
                {
                    ["Plugins.Payments.StripeApplePay.Instructions"] = "\t<p> For plugin configuration follow these steps:<br /> <br /> 1. If you haven't already, create an account on Stripe.com and sign in<br /> 2. In the Developers menu (left), choose the API Keys option. 3. You will see two keys listed, a Publishable key and a Secret key. You will need both. (If you'd like, you can create and use a set of restricted keys. That topic isn't covered here.) <em>Stripe supports test keys and production keys. Use whichever pair is appropraite. There's no switch between test/sandbox and proudction other than using the appropriate keys.</em> 4. Paste these keys into the configuration page of this plug-in. (Both keys are required.) <br /> <em>Note: If using production keys, the payment form will only work on sites hosted with HTTPS. (Test keys can be used on http sites.) If using test keys, use these <a href='https://stripe.com/docs/testing'>test card numbers from Stripe</a>.</em><br /> </p>",
                    ["Plugins.Payments.StripeApplePay.PaymentMethodDescription"] = "Payment by Credit/Debit card",
                    ["Plugins.Payments.StripeApplePay.AccountInfo"] = "Define Your Stripe Api Information",
                    ["Plugins.Payments.StripeApplePay.Fields.PublishableKey"] = "PublishableKey Api Key",
                    ["Plugins.Payments.StripeApplePay.Fields.PublishableKey.Hint"] = "Enter your PublishableKey Api Key information on your Stripe control panel.",
                    ["Plugins.Payments.StripeApplePay.Fields.SecretKey"] = "Api Secret Key",
                    ["Plugins.Payments.StripeApplePay.Fields.SecretKey.Hint"] = "Enter your Api Secret information on your Stripe control panel.",
                    ["Plugins.Payments.StripeApplePay.VirtualPosInfo"] = "Define Your Stripe Payment Settings",
                    ["Plugins.Payments.StripeApplePay.Fields.PaymentFailed"] = "Payment Failed",
                    ["Plugins.Payments.StripeApplePay.Fields.PaymentErrors"] = "Payment Errors",
                    ["Plugins.Payments.StripeApplePay.Fields.refundIdTxt"] = "Stripe Refund Id : ",
                    ["Plugins.Payments.StripeApplePay.Fields.transactionIdTxt"] = "Transaction Id : ",
                    ["Plugins.Payments.StripeApplePay.Fields.refundAmountTxt"] = "Refund Amount : ",
                    ["Plugins.Payments.StripeApplePay.Fields.PaymentFailed.Order"] = "Payment Failed. Order Number #",
                    ["Plugins.Payments.StripeApplePay.Fields.Fraoud.Fail"] = "The payment was not accepted because the fraud risk of the transaction is high. Order #",
                    ["Plugins.Payments.StripeApplePay.Fields.Fraoud.Review"] = "Since there is a Fraud risk related to the transaction, the payment has been taken under review. Order #",
                    ["Plugins.Payments.StripeApplePay.Fields.PaymentIntent.NotFound"] = "Please select and complete your payment using Google Pay, Apple Pay, or another wallet before proceeding with the checkout process.",
                    ["Plugins.Payments.StripeApplePay.Fields.PaymentMethod.NotFound"] = "Please select and complete your payment using Google Pay, Apple Pay, or another wallet before proceeding with the checkout process."
                });
            }
            await base.InstallAsync();
        }

        public override async Task UninstallAsync()
        {
            //settings
            await _settingService.DeleteSettingAsync<StripeApplePayPaymentSettings>();

            //locales
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Payments.Stripe");
            await base.UninstallAsync();
        }

        // IPaymentMethod üyelerinin implementasyonu
        public async Task<bool> CanRePostProcessPaymentAsync(Nop.Core.Domain.Orders.Order order) => false;
        public async Task<CancelRecurringPaymentResult> CancelRecurringPaymentAsync(CancelRecurringPaymentRequest cancelPaymentRequest) => throw new NotImplementedException();
        public async Task<CapturePaymentResult> CaptureAsync(CapturePaymentRequest capturePaymentRequest) => throw new NotImplementedException();
        public bool SupportCapture => false;
        public bool SupportPartiallyRefund => true;
        public bool SupportRefund => true;
        public bool SupportVoid => true;
        public async Task<decimal> GetAdditionalHandlingFeeAsync(IList<ShoppingCartItem> cart) => 0;
        public Task<IList<string>> ValidatePaymentFormAsync(IFormCollection form)
        {
            var warnings = Task.FromResult<IList<string>>(new List<string>());


            //validate
            var validator = new PaymentInfoValidator(this._localizationService);
            var model = new PaymentInfoModel
            {
                //PaymentIntentId = form["PaymentIntentId"],
                PaymentMethodId=form["PaymentMethodId"]
            };

            var result = new List<string>();

            var validationResult = validator.Validate(model);
            if (!validationResult.IsValid)
            {
                // Tek bir genel hata mesajı ekle
                result.Add("Please select and complete your payment using Google Pay, Apple Pay, or another wallet before proceeding with the checkout process.");
                warnings = Task.FromResult<IList<string>>(result);
            }


            return warnings;
        }
        public async Task<ProcessPaymentRequest> GetPaymentInfoAsync(IFormCollection form)
        {
            var paymentRequest = new ProcessPaymentRequest();


            form.TryGetValue("PaymentMethodId", out StringValues PaymentMethodId);
            //form.TryGetValue("PaymentIntentId", out StringValues PaymentIntentId);

            paymentRequest.CustomValues.Add("PaymentMethodId", PaymentMethodId.ToString());
           // paymentRequest.CustomValues.Add("PaymentIntentId", PaymentIntentId.ToString());

            return paymentRequest;
        }
        public async Task<bool> HidePaymentMethodAsync(IList<ShoppingCartItem> cart) => false;
        public PaymentMethodType PaymentMethodType => PaymentMethodType.Standard;
        public RecurringPaymentType RecurringPaymentType => RecurringPaymentType.NotSupported;
        /// <summary>
        /// Refunds a payment
        /// </summary>
        /// <param name="refundPaymentRequest">Request</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the result
        /// </returns>
        public async Task<RefundPaymentResult> RefundAsync(RefundPaymentRequest refundPaymentRequest)
        {
            var result = new RefundPaymentResult();

            var currency = await _currencyService.GetCurrencyByCodeAsync(refundPaymentRequest.Order.CustomerCurrencyCode);

            var convertedCurrency = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(refundPaymentRequest.AmountToRefund, currency);
            if (!refundPaymentRequest.IsPartialRefund)
            {
                var service = new RefundService();

                var refundOpt = new RefundCreateOptions();

                refundOpt.Charge = refundPaymentRequest.Order.AuthorizationTransactionId.ToString();

                var refund = await service.CreateAsync(refundOpt, GetStripeApiRequestOptions());


                if (refund.Status == "succeeded")
                {
                    result.NewPaymentStatus = PaymentStatus.Refunded;
                    try
                    {


                        var resTxt = "Refund Id:" + refund.Id + Environment.NewLine +
                                     "Balance Transaction Id:" + refund.BalanceTransactionId + Environment.NewLine +
                                     "Amount:" + refund.Amount / 100 + refund.Currency;
                        List<string> PaymentInformation = new()
                        {
                            resTxt

                        };
                        //order note
                        await _orderService.InsertOrderNoteAsync(new OrderNote
                        {
                            OrderId = refundPaymentRequest.Order.Id,
                            Note = string.Join(" | ", PaymentInformation),
                            DisplayToCustomer = false,
                            CreatedOnUtc = DateTime.UtcNow

                        });

                        try
                        {
                            refundPaymentRequest.Order.OrderStatus = OrderStatus.Cancelled;
                            await _orderService.UpdateOrderAsync(refundPaymentRequest.Order);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);

                        }


                    }
                    catch
                    {

                    }
                }
                else
                {
                    result.Errors.Add(refund.FailureReason);
                }
            }
            else
            {
                var service = new RefundService();

                var refundOpt = new RefundCreateOptions();
                refundOpt.Amount = (long)(convertedCurrency * 100);
                refundOpt.Charge = refundPaymentRequest.Order.AuthorizationTransactionId.ToString();



                var refund = await service.CreateAsync(refundOpt, GetStripeApiRequestOptions());

                if (refund.Status == "succeeded")
                {
                    result.NewPaymentStatus = PaymentStatus.PartiallyRefunded;
                    try
                    {





                        List<string> PaymentInformation = new List<string>();
                        PaymentInformation.Add("Refund Id:" + refund.Id);
                        PaymentInformation.Add("Balance Transaction Id:" + refund.BalanceTransactionId);
                        PaymentInformation.Add("Amount:" + refund.Amount / 100 + refund.Currency);
                        //order note
                        await _orderService.InsertOrderNoteAsync(new OrderNote
                        {
                            OrderId = refundPaymentRequest.Order.Id,
                            Note = string.Join(" | ", PaymentInformation),
                            DisplayToCustomer = false,
                            CreatedOnUtc = DateTime.UtcNow
                        });






                    }
                    catch
                    {

                    }
                }
                else
                {
                    result.Errors.Add(refund.FailureReason);
                }
            }

            return await Task.FromResult(result);
        }

        public async Task<ProcessPaymentResult> ProcessRecurringPaymentAsync(ProcessPaymentRequest processPaymentRequest) => throw new NotImplementedException();
        /// <summary>
        /// Voids a payment
        /// </summary>
        /// <param name="voidPaymentRequest">Request</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the result
        /// </returns>
        public async Task<VoidPaymentResult> VoidAsync(VoidPaymentRequest voidPaymentRequest)
        {
            var result = new VoidPaymentResult();
            var service = new RefundService();

            var refundOpt = new RefundCreateOptions();
            refundOpt.Charge = voidPaymentRequest.Order.AuthorizationTransactionId.ToString();




            var refund = await service.CreateAsync(refundOpt, GetStripeApiRequestOptions());

            if (refund.Status == "succeeded")
            {
                result.NewPaymentStatus = PaymentStatus.Voided;
                try
                {





                    List<string> PaymentInformation = new List<string>();
                    PaymentInformation.Add("Refund Id:" + refund.Id);
                    PaymentInformation.Add("Balance Transaction Id:" + refund.BalanceTransactionId);
                    PaymentInformation.Add("Amount:" + refund.Amount);
                    //order note
                    await _orderService.InsertOrderNoteAsync(new OrderNote
                    {
                        OrderId = voidPaymentRequest.Order.Id,
                        Note = string.Join(" | ", PaymentInformation),
                        DisplayToCustomer = false,
                        CreatedOnUtc = DateTime.UtcNow
                    });






                }
                catch
                {

                }
            }
            else
            {
                result.Errors.Add(refund.FailureReason);
            }

            return await Task.FromResult(result);
        }
        public string GetPublicViewComponentName() => "StripeApplePay";
        public async Task<string> GetPaymentMethodDescriptionAsync() => "Pay with Apple Pay/Google Pay using Stripe.";

        public Type GetPublicViewComponent()
        {
            return typeof(StripeApplePayViewComponent);
        }

        public bool SkipPaymentInfo => false;

       // public bool HideInWidgetList =>false;
    }

  
}
