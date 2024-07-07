using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Nop.Core.Domain.Directory;
using Nop.Core;
using Nop.Services.Catalog;
using Nop.Services.Cms;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Plugins;
using Nop.Services.ScheduleTasks;
using Nop.Services.Tax;
using Nop.Web.Framework.Infrastructure;
using Microsoft.Extensions.Primitives;
using Nop.Core.Domain.Localization;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Services.Customers;
using System.Threading.Tasks;
using System;
using System.Linq;
using Nop.Plugin.Payments.StripeApplePay.Models;
using Nop.Plugin.Payments.StripeApplePay.Services;
using Stripe;
using CustomerService = Nop.Services.Customers.CustomerService;
using LinqToDB.Common;
using Stripe.Climate;
using Newtonsoft.Json;
using Nop.Plugin.Payments.StripeApplePay.Controllers;
using System.IO;

namespace Nop.Plugin.Payments.StripeApplePay
{
    /// <summary>
    /// Rename this file and change to the correct type
    /// </summary>
    public class StripeApplePayPlugin : BasePlugin, IPaymentMethod
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IWebHelper _webHelper;
        private readonly IShoppingCartService _shoppingCartService;
        private readonly IOrderTotalCalculationService _orderTotalCalculationService;
        private readonly IWorkContext _workContext;
        private readonly IPaymentStripeApplePayService _paymentStripeService;
        private readonly StripeApplePayPaymentSettings _stripePaymentSettings;
        private readonly ICurrencyService _currencyService;
        private readonly IStoreContext _storeContext;
        private readonly ILanguageService _languageService;
        private readonly ILocalizationService _localizationService;

        public StripeApplePayPlugin(
            IHttpContextAccessor httpContextAccessor,
            IWebHelper webHelper,
            IShoppingCartService shoppingCartService,
            IOrderTotalCalculationService orderTotalCalculationService,
            IWorkContext workContext,
            IPaymentStripeApplePayService paymentStripeService,
            StripeApplePayPaymentSettings stripePaymentSettings,
            ICurrencyService currencyService,
            IStoreContext storeContext,
            ILanguageService languageService,
            ILocalizationService localizationService)
        {
            _httpContextAccessor = httpContextAccessor;
            _webHelper = webHelper;
            _shoppingCartService = shoppingCartService;
            _orderTotalCalculationService = orderTotalCalculationService;
            _workContext = workContext;
            _paymentStripeService = paymentStripeService;
            _stripePaymentSettings = stripePaymentSettings;
            _currencyService = currencyService;
            _storeContext = storeContext;
            _languageService = languageService;
            _localizationService = localizationService;
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
        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation()}Admin/PaymentStripeApplePay/Configure";
        }
        public async Task<ProcessPaymentResult> ProcessPaymentAsync(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();

            try
            {
                StripeConfiguration.ApiKey = _stripePaymentSettings.SecretKey;

                var customer = await _paymentStripeService.GetBuyer(processPaymentRequest.CustomerId);
                if (customer == null || string.IsNullOrEmpty(customer.Id))
                    throw new Exception("No Valid Customer Found!");

                var store = await _storeContext.GetCurrentStoreAsync();
                var cart = await _shoppingCartService.GetShoppingCartAsync(customer.Customer, ShoppingCartType.ShoppingCart, store.Id);
                if (!cart.Any())
                    throw new Exception("No Product Found in Your Cart!");

                if (string.IsNullOrEmpty(customer.billingAddress.Address1))
                    throw new NopException("Customer billing address not set!");

                if (string.IsNullOrEmpty(customer.shippinAddress.Address1))
                    throw new NopException("Customer shipping address not set!");

                var currency = await _workContext.GetWorkingCurrencyAsync();
                var shoppingCartTotal = await _orderTotalCalculationService.GetShoppingCartTotalAsync(cart, true);
                var shoppingCartUnitPriceWithDiscount = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(shoppingCartTotal.shoppingCartTotal.Value, currency);


                StripeConfiguration.ApiKey = _stripePaymentSettings.SecretKey;

                  var paymentMethodOptions = new PaymentMethodDomainCreateOptions { DomainName = "hoodarcheryshop.com" };
                var paymentMethodDomainService = new PaymentMethodDomainService();
               var paymentMethodDomain =await paymentMethodDomainService.CreateAsync(paymentMethodOptions);
               


             var paymentIntentService = new PaymentIntentService();
                var options = new PaymentIntentCreateOptions
                {
                    Amount = (long)(shoppingCartUnitPriceWithDiscount * 100), // Total amount in cents
                    Currency = currency.CurrencyCode.ToLower(),
                    PaymentMethodTypes = new List<string> { "card" }
                
                };
                

                var intent = await paymentIntentService.CreateAsync(options, GetStripeApiRequestOptions());

                result.NewPaymentStatus = PaymentStatus.Pending;
                result.AuthorizationTransactionId = intent.Id;
                result.AuthorizationTransactionResult = $"PaymentIntent created with ID: {intent.Id}";
                processPaymentRequest.CustomValues.Add("StripePaymentIntentId", intent.Id);
            }
            catch (Exception ex)
            {
                result.AddError(ex.Message);
            }

            return result;
        }

        public async Task PostProcessPaymentAsync(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            var httpContext = _httpContextAccessor.HttpContext;

            // JSON içeriği okuma
            string requestBody;
            using (var streamReader = new StreamReader(httpContext.Request.Body))
            {
                requestBody = await streamReader.ReadToEndAsync();
            }

            // JSON içeriğini deserializasyon yapma
            var paymentData = JsonConvert.DeserializeObject<ConfirmPaymentRequest>(requestBody);
            var paymentIntentId = paymentData.PaymentIntentId;

            StripeConfiguration.ApiKey = _stripePaymentSettings.SecretKey;

            var paymentIntentService = new PaymentIntentService();
            var paymentIntent = await paymentIntentService.GetAsync(paymentIntentId, null, GetStripeApiRequestOptions());

            if (paymentIntent.Status == "succeeded")
            {
                postProcessPaymentRequest.Order.PaymentStatus = PaymentStatus.Paid;
                postProcessPaymentRequest.Order.OrderStatus = OrderStatus.Complete;
                postProcessPaymentRequest.Order.AuthorizationTransactionId = paymentIntent.Id;
                postProcessPaymentRequest.Order.AuthorizationTransactionResult = $"PaymentIntent succeeded with ID: {paymentIntent.Id}";
            }
            else
            {
                throw new NopException($"PaymentIntent failed with status: {paymentIntent.Status}");
            }
        }

        public override async Task InstallAsync()
        {

            //locales
            bool languageInstalled = false;
            var languages = await _languageService.GetAllLanguagesAsync();
            Language enLanguage = null;
            Language trLanguage = null;
            if (languages.Count > 0)
            {
                foreach (var language in languages)
                {
                    if (language.UniqueSeoCode == "en")
                    {
                        enLanguage = language;
                    }
                    else if (language.UniqueSeoCode == "tr")
                    {
                        trLanguage = language;
                    }

                }
            }






            if (enLanguage != null)
            {
                await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
                {
                    ["Plugins.Payments.StripeApplePay.Instructions"] = "You can edit the settings of your Stripe virtual pos integration.",
                    ["Plugins.Payments.StripeApplePay.PaymentMethodDescription"] = "Payment by Credit/Debit card",
                    ["Plugins.Payments.StripeApplePay.AccountInfo"] = "Define Your Stripe Api Information",
                    ["Plugins.Payments.StripeApplePay.Fields.PublishableKey"] = "PublishableKey Api Key",
                    ["Plugins.Payments.StripeApplePay.Fields.PublishableKey.Hint"] = "Enter your PublishableKey Api Key information on your Stripe control panel.",
                    ["Plugins.Payments.StripeApplePay.Fields.SecretKey"] = "Api Secret Key",
                    ["Plugins.Payments.StripeApplePay.Fields.SecretKey.Hint"] = "Enter your Api Secret information on your Stripe control panel.",
                    ["Plugins.Payments.StripeApplePay.VirtualPosInfo"] = "Define Your Stripe Payment Settings",
                    ["Plugins.Payments.StripeApplePay.Fields.IsCardStorage"] = "Store Card Information",
                    ["Plugins.Payments.StripeApplePay.Fields.IsCardStorage.Hint"] = "This option stores the first six digits and the last four digits of the credit card information transmitted by Stripe in the database (not sent to any third party processors).",
                    ["Plugins.Payments.StripeApplePay.Fields.PaymentFailed"] = "Payment Failed",
                    ["Plugins.Payments.StripeApplePay.Fields.PaymentErrors"] = "Payment Errors",
                    ["Plugins.Payments.StripeApplePay.Fields.refundIdTxt"] = "Stripe Refund Id : ",
                    ["Plugins.Payments.StripeApplePay.Fields.transactionIdTxt"] = "Transaction Id : ",
                    ["Plugins.Payments.StripeApplePay.Fields.refundAmountTxt"] = "Refund Amount : ",
                    ["Plugins.Payments.StripeApplePay.Fields.PaymentFailed.Order"] = "Payment Failed. Order Number #",
                    ["Plugins.Payments.StripeApplePay.Fields.Fraoud.Fail"] = "The payment was not accepted because the fraud risk of the transaction is high. Order #",
                    ["Plugins.Payments.StripeApplePay.Fields.Fraoud.Review"] = "Since there is a Fraud risk related to the transaction, the payment has been taken under review. Order #",
                    ["Plugins.Payments.StripeApplePay.Fields.Order.NotFound"] = "Order Not Found! Order #"
                }, enLanguage.Id);
                languageInstalled = true;
            }



            if (languageInstalled == false)
            {
                //Default Fields
                await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
                {
                    ["Plugins.Payments.StripeApplePay.Instructions"] = "You can edit the settings of your Stripe virtual pos integration.",
                    ["Plugins.Payments.StripeApplePay.PaymentMethodDescription"] = "Payment by Credit/Debit card",
                    ["Plugins.Payments.StripeApplePay.AccountInfo"] = "Define Your Stripe Api Information",
                    ["Plugins.Payments.StripeApplePay.Fields.PublishableKey"] = "PublishableKey Api Key",
                    ["Plugins.Payments.StripeApplePay.Fields.PublishableKey.Hint"] = "Enter your PublishableKey Api Key information on your Stripe control panel.",
                    ["Plugins.Payments.StripeApplePay.Fields.SecretKey"] = "Api Secret Key",
                    ["Plugins.Payments.StripeApplePay.Fields.SecretKey.Hint"] = "Enter your Api Secret information on your Stripe control panel.",
                    ["Plugins.Payments.StripeApplePay.VirtualPosInfo"] = "Define Your Stripe Payment Settings",
                    ["Plugins.Payments.StripeApplePay.Fields.IsCardStorage"] = "Store Card Information",
                    ["Plugins.Payments.StripeApplePay.Fields.IsCardStorage.Hint"] = "This option stores the first six digits and the last four digits of the credit card information transmitted by Stripe in the database (not sent to any third party processors).",
                    ["Plugins.Payments.StripeApplePay.Fields.PaymentFailed"] = "Payment Failed",
                    ["Plugins.Payments.StripeApplePay.Fields.PaymentErrors"] = "Payment Errors",
                    ["Plugins.Payments.StripeApplePay.Fields.refundIdTxt"] = "Stripe Refund Id : ",
                    ["Plugins.Payments.StripeApplePay.Fields.transactionIdTxt"] = "Transaction Id : ",
                    ["Plugins.Payments.StripeApplePay.Fields.refundAmountTxt"] = "Refund Amount : ",
                    ["Plugins.Payments.StripeApplePay.Fields.PaymentFailed.Order"] = "Payment Failed. Order Number #",
                    ["Plugins.Payments.StripeApplePay.Fields.Fraoud.Fail"] = "The payment was not accepted because the fraud risk of the transaction is high. Order #",
                    ["Plugins.Payments.StripeApplePay.Fields.Fraoud.Review"] = "Since there is a Fraud risk related to the transaction, the payment has been taken under review. Order #",
                    ["Plugins.Payments.StripeApplePay.Fields.Order.NotFound"] = "Order Not Found! Order #"
                });
            }
            await base.InstallAsync();
        }

        public override async Task UninstallAsync()
        {
            await base.UninstallAsync();
        }

        #region Properties

        /// <summary>
        /// Gets a value indicating whether capture is supported
        /// </summary>
        public bool SupportCapture => false;

        /// <summary>
        /// Gets a value indicating whether partial refund is supported
        /// </summary>
        public bool SupportPartiallyRefund => true;

        /// <summary>
        /// Gets a value indicating whether refund is supported
        /// </summary>
        public bool SupportRefund => true;

        /// <summary>
        /// Gets a value indicating whether void is supported
        /// </summary>
        public bool SupportVoid => true;

        /// <summary>
        /// Gets a recurring payment type of payment method
        /// </summary>
        public RecurringPaymentType RecurringPaymentType => RecurringPaymentType.NotSupported;

        /// <summary>
        /// Gets a payment method type
        /// </summary>
        public PaymentMethodType PaymentMethodType { get; set; } = PaymentMethodType.Redirection;

        /// <summary>
        /// Gets a value indicating whether we should display a payment information page for this plugin
        /// </summary>
        public bool SkipPaymentInfo => false;

        #endregion
        public async Task<string> GetPaymentMethodDescriptionAsync()
        {
            return await _localizationService.GetResourceAsync("Plugins.Payments.StripeApplePay.PaymentMethodDescription");
        }
        public string PaymentMethodDescription => "Pay with Apple Pay using Stripe.";



        public  Task<VoidPaymentResult> VoidAsync(VoidPaymentRequest voidPaymentRequest)
        {
            return Task.FromResult(new VoidPaymentResult { Errors = new[] { "Void method not supported" } });

        }
        public Task<RefundPaymentResult> RefundAsync(RefundPaymentRequest refundPaymentRequest)
        {
            return Task.FromResult(new RefundPaymentResult { Errors = new[] { "Refund method not supported" } });
        }


        /// <summary>
        /// Captures payment
        /// </summary>
        /// <param name="capturePaymentRequest">Capture payment request</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the capture payment result
        /// </returns>
        public Task<CapturePaymentResult> CaptureAsync(CapturePaymentRequest capturePaymentRequest)
        {
            return Task.FromResult(new CapturePaymentResult { Errors = new[] { "Capture method not supported" } });
        }


        /// <summary>
        /// Returns a value indicating whether payment method should be hidden during checkout
        /// </summary>
        /// <param name="cart">Shopping cart</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the rue - hide; false - display.
        /// </returns>
        public Task<bool> HidePaymentMethodAsync(IList<ShoppingCartItem> cart)
        {
            //you can put any logic here
            //for example, hide this payment method if all products in the cart are downloadable
            //or hide this payment method if current customer is from certain country
            return Task.FromResult(false);
        }

        /// <summary>
        /// Get payment information
        /// </summary>
        /// <param name="form">The parsed form values</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the payment info holder
        /// </returns>
        public Task<ProcessPaymentRequest> GetPaymentInfoAsync(IFormCollection form)
        {
            return Task.FromResult(new ProcessPaymentRequest());
        }

        /// <summary>
        /// Gets a name of a view component for displaying plugin in public store ("payment info" checkout step)
        /// </summary>
        /// <returns>View component name</returns>
        public string GetPublicViewComponentName()
        {
            return StripeApplePayPaymentDefaults.ViewComponentName;
        }
        /// <summary>
        /// Process recurring payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the process payment result
        /// </returns>
        public Task<ProcessPaymentResult> ProcessRecurringPaymentAsync(ProcessPaymentRequest processPaymentRequest)
        {
            return Task.FromResult(new ProcessPaymentResult { Errors = new[] { "Process Recurring Payment not supported" } });
        }

        /// <summary>
        /// Gets additional handling fee
        /// </summary>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the additional handling fee
        /// </returns>
        public async Task<decimal> GetAdditionalHandlingFeeAsync(IList<ShoppingCartItem> cart)
        {

            return await _orderTotalCalculationService.CalculatePaymentAdditionalFeeAsync(cart, _stripePaymentSettings.AdditionalFee, _stripePaymentSettings.AdditionalFeePercentage);
        }

        /// <summary>
        /// Cancels a recurring payment
        /// </summary>
        /// <param name="cancelPaymentRequest">Request</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the result
        /// </returns>
        public Task<CancelRecurringPaymentResult> CancelRecurringPaymentAsync(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            //always success
            return Task.FromResult(new CancelRecurringPaymentResult());
        }

 
        private bool IsStripeTokenID(string token)
        {
            return token.StartsWith("tok_");
        }

        /// <summary>
        /// Validate payment form
        /// </summary>
        /// <param name="form">The parsed form values</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the list of validating errors
        /// </returns>
        public Task<IList<string>> ValidatePaymentFormAsync(IFormCollection form)
        {
            return Task.FromResult<IList<string>>(new List<string>());
        }
        /// <summary>
        /// Gets a value indicating whether customers can complete a payment after order is placed but not completed (for redirection payment methods)
        /// </summary>
        /// <param name="order">Order</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the result
        /// </returns>
        public Task<bool> CanRePostProcessPaymentAsync(Core.Domain.Orders.Order order)
        {
            if (order == null)
                throw new ArgumentNullException(nameof(order));

            //it's not a redirection payment method. So we always return false
            return Task.FromResult(false);
        }


    }
}
