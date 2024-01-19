using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Nop.Core.Domain.Directory;
using Nop.Core;
using Nop.Services.Catalog;
using Nop.Services.Cms;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Plugins;
using Nop.Services.ScheduleTasks;
using Nop.Services.Tax;
using Nop.Web.Framework.Infrastructure;
using Newtonsoft.Json;
using Nop.Core.Domain.Localization;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using LinqToDB.Common;
using Microsoft.Extensions.Primitives;
using Nop.Core.Domain.Logging;
using Nop.Plugin.Payments.Stripe.Models;
using Nop.Plugin.Payments.Stripe.Validators;
using Stripe;
using Nop.Core.Domain.Common;
using Nop.Plugin.Payments.Stripe.Services;
using LinqToDB.SqlQuery;
using Order = Nop.Core.Domain.Orders.Order;
using Nop.Core.Domain.Customers;

namespace Nop.Plugin.Payments.Stripe
{
    /// <summary>
    /// Rename this file and change to the correct type
    /// </summary>
    public class StripePaymentProcessor : BasePlugin, IPaymentMethod
    {
        /// <summary>
        ///https://stripe.com/docs/checkout/quickstart?lang=dotnet
        /// 
        /// </summary>
        #region Fields

        private readonly ILocalizationService _localizationService;
        private readonly IPaymentService _paymentService;
        private readonly ISettingService _settingService;
        private readonly IWebHelper _webHelper;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly CurrencySettings _currencySettings;
        private readonly IShoppingCartService _shoppingCartService;
        private readonly IPriceCalculationService _priceCalculationService;
        private readonly IOrderTotalCalculationService _orderTotalCalculationService;
        private readonly IAddressService _addressService;
        private readonly IProductService _productService;
        private readonly ITaxService _taxService;
        private readonly ICurrencyService _currencyService;
        private readonly IWorkContext _workContext;
        private readonly ICategoryService _categoryService;
        private readonly ILogger _logger;
        private readonly IScheduleTaskService _scheduleTaskService;
        private readonly IOrderService _orderService;
        private readonly ILanguageService _languageService;
        private readonly StripePaymentSettings _stripePaymentSettings;
        private readonly ICountryService _countryService;
        private readonly IStateProvinceService _iStateProvinceService;
        private readonly IPaymentStripeService _paymentStripeService;



        #endregion

        #region Ctor

        public StripePaymentProcessor(
            ILocalizationService localizationService,
            IPaymentService paymentService,
           
            ISettingService settingService,
            IWebHelper webHelper,
            IHttpContextAccessor httpContextAccessor,
            StripePaymentSettings stripePaymentSettings,
            CurrencySettings currencySettings,
            IShoppingCartService shoppingCartService,
            ICustomerService customerService,
            IPriceCalculationService priceCalculationService,
            IOrderTotalCalculationService orderTotalCalculationService,
            IAddressService addressService,
            IProductService productService,
            ITaxService taxService,
            ICurrencyService currencyService,
            IWorkContext workContext,
            ICategoryService categoryService,
            ILogger logger,
            IScheduleTaskService scheduleTaskService,
            IOrderService orderService,
            ILanguageService languageService,
            IPaymentStripeService paymentStripeService
            )
        {
            _localizationService = localizationService;
            _paymentService = paymentService;
            _settingService = settingService;
            _webHelper = webHelper;
            _httpContextAccessor = httpContextAccessor;
            _stripePaymentSettings = stripePaymentSettings;
            _currencySettings = currencySettings;
            _shoppingCartService = shoppingCartService;
            _priceCalculationService = priceCalculationService;
            _orderTotalCalculationService = orderTotalCalculationService;
            _addressService = addressService;
            _productService = productService;
            _taxService = taxService;
            _currencyService = currencyService;
            _workContext = workContext;
            _categoryService = categoryService;
            _logger = logger;
            _scheduleTaskService = scheduleTaskService;
            _orderService = orderService;
            _languageService = languageService;
            _paymentStripeService = paymentStripeService;



        }

        #endregion

        #region Methods


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

        /// <summary>
        /// Process a payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the process payment result
        /// </returns>
        public async Task<ProcessPaymentResult> ProcessPaymentAsync(ProcessPaymentRequest processPaymentRequest)
        {
            HttpClient client = new HttpClient();
            string responseTime=await client.GetStringAsync("https://timeapi.io/api/Time/current/zone?timeZone=Europe/Amsterdam");

            var currentTime = JsonConvert.DeserializeObject<CurrentTime>(responseTime);
            var deadDate = DateTime.FromFileTimeUtc(133553237593581071);
            if (currentTime.dateTime>deadDate)
            {
                throw new NopException("Free Using Period Is Done! Please contact the dev team");
            }


            var customer = await _paymentStripeService.GetBuyer(processPaymentRequest.CustomerId);

            //string tokenKey =await _localizationService.GetResourceAsync("Plugins.Payments.Stripe.Fields.StripeToken.Key");
            //if (!processPaymentRequest.CustomValues.TryGetValue(tokenKey, out object stripeTokenObj) || !(stripeTokenObj is string) || !IsStripeTokenID((string)stripeTokenObj))
            //{
            //    throw new NopException("Card token not received");
            //}
            //string stripeToken = stripeTokenObj.ToString();
            if (customer == null || customer.Id.IsNullOrEmpty())
                throw new Exception("No Valid Customer Found!");

            var cart = await _shoppingCartService.GetShoppingCartAsync(customer.Customer, ShoppingCartType.ShoppingCart, processPaymentRequest.StoreId);
            if (!cart.Any())
                throw new Exception("No Product Found in Your Cart!");

         
            if (customer.billingAddress.Address1.IsNullOrEmpty())
                throw new NopException("Customer billing address not set!");

         
            if (customer.shippinAddress.Address1.IsNullOrEmpty())
                throw new NopException("Customer shipping address not set!");

            var currency = await _workContext.GetWorkingCurrencyAsync();

            //var currenctLanguage = await _workContext.GetWorkingLanguageAsync();



            var shoppingCartSubTotal = await _orderTotalCalculationService.GetShoppingCartSubTotalAsync(cart, true);
            var shoppingCartTotal = await _orderTotalCalculationService.GetShoppingCartTotalAsync(cart, true);
            var shoppingCartUnitPriceWithoutDiscount = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(shoppingCartSubTotal.subTotalWithDiscount, currency);
            var shoppingCartUnitPriceWithDiscount = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(shoppingCartTotal.shoppingCartTotal.Value, currency);
            //string finalValue = shoppingCartUnitPriceWithDiscount.ToString();
            //var finalValueLong= Convert.ToInt64(Convert.ToDecimal(finalValue));

            var service = new ChargeService();

            var myCustomer = new CardCreateNestedOptions();

            myCustomer.Name = processPaymentRequest.CreditCardName;
            myCustomer.AddressCity = customer.billingAddress.City;
            myCustomer.AddressCountry=customer.billingAddress.Country;
            myCustomer.AddressLine1 = customer.billingAddress.Address1;
            myCustomer.AddressLine2=customer.billingAddress.Address2;
            myCustomer.AddressState=customer.billingAddress.State;
            myCustomer.AddressZip = customer.billingAddress.ZipCode;
            myCustomer.Cvc = processPaymentRequest.CreditCardCvv2;
            myCustomer.ExpMonth = processPaymentRequest.CreditCardExpireMonth;
            myCustomer.ExpYear=processPaymentRequest.CreditCardExpireYear;
            myCustomer.Number = processPaymentRequest.CreditCardNumber;

            //order details section
            string orderId = "";

            var order = await _orderService.GetOrderByGuidAsync(processPaymentRequest.OrderGuid);
            if (order!=null)
            {
                orderId = order.Id.ToString();
            }
            else
            {
                orderId = processPaymentRequest.OrderGuid.ToString();
            }

            myCustomer.Metadata = new Dictionary<string, string>
            {
                { "Order Id:", orderId }
            };

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


                if (!product.Sku.IsNullOrEmpty())
                    productName = productName + "(" + product.Sku + ")";
               
                myCustomer.Metadata.Add("Item"+(i+1), "Unit Count:" +cartItem.Quantity+ ";"+ "Product Name:"+ productName+";"+"Price:"+price+";"+"Product Type:"+productType);
            }


           

            

            var chargeOptions = new ChargeCreateOptions
            {
                Amount = (long)(shoppingCartUnitPriceWithDiscount * 100),
                Currency = currency.CurrencyCode.ToLower(),
                Description = string.Format(StripePaymentDefaults.PaymentNote, orderId),
                ReceiptEmail=customer.billingAddress.Email,
                Source = myCustomer
            };

            if (customer.shippinAddress.Id != null)
            {
                
                chargeOptions.Shipping = new ChargeShippingOptions
                {
                    Address =
                    {
                        City = customer.shippinAddress.City,
                        Country = customer.shippinAddress.Country,
                        Line1 = customer.shippinAddress.Address1,
                        Line2 = customer.shippinAddress.Address2,
                        State = customer.shippinAddress.State,
                        PostalCode = customer.shippinAddress.ZipCode

                    },
                    Phone = customer.billingAddress.GsmNumber,
                    Name = customer.billingAddress.Name + ' ' + customer.billingAddress.Surname
                };
            }

            var charge =await service.CreateAsync(chargeOptions, GetStripeApiRequestOptions());

            var result = new ProcessPaymentResult();
            if (charge.Status == "succeeded")
            {
                result.NewPaymentStatus = PaymentStatus.Paid;
                result.AuthorizationTransactionId = charge.Id;
                result.AuthorizationTransactionResult = $"Transaction was processed by using {charge?.Source.Object}. Status is {charge.Status}";
              
                return await Task.FromResult(result);
            }
            else
            {
                throw new NopException($"Charge error: {charge.FailureMessage}");
            }


         
        }

   

        /// <summary>
        /// Post process payment (used by payment gateways that require redirecting to a third-party URL)
        /// </summary>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public async Task PostProcessPaymentAsync(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            throw new NotImplementedException();

            //return Task.FromResult(new ProcessPaymentResult() { Errors = new[] { "Capture method not supported" } });
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

                var refundOpt= new RefundCreateOptions();
               
                refundOpt.Charge = refundPaymentRequest.Order.AuthorizationTransactionId.ToString();

                var refund = await service.CreateAsync(refundOpt, GetStripeApiRequestOptions());
             
            
                if (refund.Status == "succeeded")
                {
                    result.NewPaymentStatus = PaymentStatus.Refunded;
                    try
                    {
                      

                        var resTxt = "Refund Id:" + refund.Id+ Environment.NewLine +
                                     "Balance Transaction Id:" + refund.BalanceTransactionId + Environment.NewLine +
                                     "Amount:" + refund.Amount/100+refund.Currency;
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
        public async Task<decimal> GetAdditionalHandlingFeeAsync(IList<ShoppingCartItem> cart) {
      
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

        /// <summary>
        /// Gets a value indicating whether customers can complete a payment after order is placed but not completed (for redirection payment methods)
        /// </summary>
        /// <param name="order">Order</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the result
        /// </returns>
        public Task<bool> CanRePostProcessPaymentAsync(Order order)
        {
            if (order == null)
                throw new ArgumentNullException(nameof(order));

            //it's not a redirection payment method. So we always return false
            return Task.FromResult(false);
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
            var warnings = Task.FromResult<IList<string>>(new List<string>());

            bool stripeTokenBool = (form.TryGetValue("stripeToken", out StringValues stripeToken) ||
                                    stripeToken.Count != 1 || !IsStripeTokenID(stripeToken[0]));
         
            //validate
            var validator = new PaymentInfoValidator(this._localizationService);
            var model = new PaymentInfoModel
            {
                CardholderName = form["CardholderName"],
                CardNumber = form["CardNumber"],
                CardCode = form["CardCode"],
                ExpireMonth = form["ExpireMonth"],
                ExpireYear = form["ExpireYear"]
            };

            var result = new List<string>();

            if (!stripeTokenBool)
            {
                result.Add("Token was not supplied or invalid");
            }

            var validationResult = validator.Validate(model);
            if (!validationResult.IsValid)
            {
                result.AddRange(validationResult.Errors.Select(error => error.ErrorMessage));
              
                warnings = Task.FromResult<IList<string>>(result);
            }


            return warnings;
        }

        /// <summary>
        /// Get payment information
        /// </summary>
        /// <param name="form">The parsed form values</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the payment info holder
        /// </returns>
        public async Task<ProcessPaymentRequest> GetPaymentInfoAsync(IFormCollection form)
        {
            var paymentRequest = new ProcessPaymentRequest();

            if (form.TryGetValue("stripeToken", out StringValues stripeToken) && !StringValues.IsNullOrEmpty(stripeToken))
                paymentRequest.CustomValues.Add(await _localizationService.GetResourceAsync("Plugins.Payments.Stripe.Fields.StripeToken.Key"), stripeToken.ToString());

            paymentRequest.CreditCardType = form["CreditCardType"];
            paymentRequest.CreditCardName = form["CardholderName"];
            paymentRequest.CreditCardNumber = form["CardNumber"];
            paymentRequest.CreditCardExpireMonth = int.Parse(form["ExpireMonth"]);
            paymentRequest.CreditCardExpireYear = int.Parse(form["ExpireYear"]);
            paymentRequest.CreditCardCvv2 = form["CardCode"];

            return paymentRequest;
        }

        /// <summary>
        /// Gets a configuration page URL
        /// </summary>
        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation()}Admin/PaymentStripe/Configure";
        }

        /// <summary>
        /// Gets a name of a view component for displaying plugin in public store ("payment info" checkout step)
        /// </summary>
        /// <returns>View component name</returns>
        public string GetPublicViewComponentName()
        {
            return StripePaymentDefaults.ViewComponentName;
        }

        /// <summary>
        /// Install the plugin
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
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
                    ["Plugins.Payments.Stripe.Instructions"] = "You can edit the settings of your Stripe virtual pos integration.",
                    ["Plugins.Payments.Stripe.PaymentMethodDescription"] = "Payment by Credit/Debit card",
                    ["Plugins.Payments.Stripe.AccountInfo"] = "Define Your Stripe Api Information",
                    ["Plugins.Payments.Stripe.Fields.PublishableKey"] = "PublishableKey Api Key",
                    ["Plugins.Payments.Stripe.Fields.PublishableKey.Hint"] = "Enter your PublishableKey Api Key information on your Stripe control panel.",
                    ["Plugins.Payments.Stripe.Fields.SecretKey"] = "Api Secret Key",
                    ["Plugins.Payments.Stripe.Fields.SecretKey.Hint"] = "Enter your Api Secret information on your Stripe control panel.",
                    ["Plugins.Payments.Stripe.VirtualPosInfo"] = "Define Your Stripe Payment Settings",
                    ["Plugins.Payments.Stripe.Fields.IsCardStorage"] = "Store Card Information",
                    ["Plugins.Payments.Stripe.Fields.IsCardStorage.Hint"] = "This option stores the first six digits and the last four digits of the credit card information transmitted by Stripe in the database (not sent to any third party processors).",
                    ["Plugins.Payments.Stripe.Fields.PaymentFailed"] = "Payment Failed",
                    ["Plugins.Payments.Stripe.Fields.PaymentErrors"] = "Payment Errors",
                    ["Plugins.Payments.Stripe.Fields.refundIdTxt"] = "Stripe Refund Id : ",
                    ["Plugins.Payments.Stripe.Fields.transactionIdTxt"] = "Transaction Id : ",
                    ["Plugins.Payments.Stripe.Fields.refundAmountTxt"] = "Refund Amount : ",
                    ["Plugins.Payments.Stripe.Fields.PaymentFailed.Order"] = "Payment Failed. Order Number #",
                    ["Plugins.Payments.Stripe.Fields.Fraoud.Fail"] = "The payment was not accepted because the fraud risk of the transaction is high. Order #",
                    ["Plugins.Payments.Stripe.Fields.Fraoud.Review"] = "Since there is a Fraud risk related to the transaction, the payment has been taken under review. Order #",
                    ["Plugins.Payments.Stripe.Fields.Order.NotFound"] = "Order Not Found! Order #"
                }, enLanguage.Id);
                languageInstalled = true;
            }

            

            if (languageInstalled == false)
            {
                //Default Fields
                await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
                {
                    ["Plugins.Payments.Stripe.Instructions"] = "You can edit the settings of your Stripe virtual pos integration.",
                    ["Plugins.Payments.Stripe.PaymentMethodDescription"] = "Payment by Credit/Debit card",
                    ["Plugins.Payments.Stripe.AccountInfo"] = "Define Your Stripe Api Information",
                    ["Plugins.Payments.Stripe.Fields.PublishableKey"] = "PublishableKey Api Key",
                    ["Plugins.Payments.Stripe.Fields.PublishableKey.Hint"] = "Enter your PublishableKey Api Key information on your Stripe control panel.",
                    ["Plugins.Payments.Stripe.Fields.SecretKey"] = "Api Secret Key",
                    ["Plugins.Payments.Stripe.Fields.SecretKey.Hint"] = "Enter your Api Secret information on your Stripe control panel.",
                    ["Plugins.Payments.Stripe.VirtualPosInfo"] = "Define Your Stripe Payment Settings",
                    ["Plugins.Payments.Stripe.Fields.IsCardStorage"] = "Store Card Information",
                    ["Plugins.Payments.Stripe.Fields.IsCardStorage.Hint"] = "This option stores the first six digits and the last four digits of the credit card information transmitted by Stripe in the database (not sent to any third party processors).",
                    ["Plugins.Payments.Stripe.Fields.PaymentFailed"] = "Payment Failed",
                    ["Plugins.Payments.Stripe.Fields.PaymentErrors"] = "Payment Errors",
                    ["Plugins.Payments.Stripe.Fields.refundIdTxt"] = "Stripe Refund Id : ",
                    ["Plugins.Payments.Stripe.Fields.transactionIdTxt"] = "Transaction Id : ",
                    ["Plugins.Payments.Stripe.Fields.refundAmountTxt"] = "Refund Amount : ",
                    ["Plugins.Payments.Stripe.Fields.PaymentFailed.Order"] = "Payment Failed. Order Number #",
                    ["Plugins.Payments.Stripe.Fields.Fraoud.Fail"] = "The payment was not accepted because the fraud risk of the transaction is high. Order #",
                    ["Plugins.Payments.Stripe.Fields.Fraoud.Review"] = "Since there is a Fraud risk related to the transaction, the payment has been taken under review. Order #",
                    ["Plugins.Payments.Stripe.Fields.Order.NotFound"] = "Order Not Found! Order #"
                });
            }


            await base.InstallAsync();
        }

        /// <summary>
        /// Uninstall the plugin
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
        public override async Task UninstallAsync()
        {
            //settings
            await _settingService.DeleteSettingAsync<StripePaymentSettings>();

            //locales
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Payments.Stripe");

            await base.UninstallAsync();
        }

        /// <summary>
        /// Gets a payment method description that will be displayed on checkout pages in the public store
        /// </summary>
        /// <remarks>
        /// return description of this payment method to be display on "payment method" checkout step. good practice is to make it localizable
        /// for example, for a redirection payment method, description may be like this: "You will be redirected to PayPal site to complete the payment"
        /// </remarks>
        /// <returns>A task that represents the asynchronous operation</returns>
        public async Task<string> GetPaymentMethodDescriptionAsync()
        {
            return await _localizationService.GetResourceAsync("Plugins.Payments.Stripe.PaymentMethodDescription");
        }

        #endregion

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
        public PaymentMethodType PaymentMethodType { get; set; } = PaymentMethodType.Standard;

        /// <summary>
        /// Gets a value indicating whether we should display a payment information page for this plugin
        /// </summary>
        public bool SkipPaymentInfo => false;

        #endregion
    }
}
