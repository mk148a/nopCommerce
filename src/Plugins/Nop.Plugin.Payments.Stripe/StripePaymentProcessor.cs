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
using DocumentFormat.OpenXml.Wordprocessing;
using Nop.Core.Domain.Catalog;
using DocumentFormat.OpenXml.Spreadsheet;
using CustomerService = Stripe.CustomerService;
using Nop.Plugin.Payments.Stripe.Components;
using Nop.Core.Domain.Messages;
using Nop.Services.Messages;
using Token = Stripe.Token;
using Nop.Core.Domain.ScheduleTasks;
using Nop.Core.Caching;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

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
        private readonly ICustomerService _customerService;
        protected readonly IStoreContext _storeContext;
        private readonly IMessageTokenProvider _messageTokenProvider;
        private readonly IQueuedEmailService _queuedEmailService;
        private readonly IEmailAccountService _emailAccountService;
        private readonly EmailAccountSettings _emailAccountSettings;
        protected readonly ITokenizer _tokenizer;
        private readonly IStaticCacheManager _staticCacheManager;

        private const string StripeThreeDsErrorPrefix = "__STRIPE_3DS__:";
        private static readonly CacheKey PendingPaymentCacheKey = new(
            "Nop.Plugin.Payments.Stripe.PendingPayment.{0}.{1}",
            "Nop.Plugin.Payments.Stripe.PendingPayment") { CacheTime = 15 };

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
            IPaymentStripeService paymentStripeService,
            IStoreContext storeContext,
            IMessageTokenProvider messageTokenProvider,
            IQueuedEmailService queuedEmailService,
            IEmailAccountService emailAccountService,
            EmailAccountSettings emailAccountSettings,
            ITokenizer tokenizer,
            IStaticCacheManager staticCacheManager

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
            _customerService = customerService;
            _storeContext = storeContext;
            _messageTokenProvider = messageTokenProvider;
            _queuedEmailService = queuedEmailService;
            _emailAccountService = emailAccountService;
            _emailAccountSettings= emailAccountSettings;
            _tokenizer= tokenizer;
            _staticCacheManager = staticCacheManager;



        }

        #endregion

        #region Methods


        /// <summary>
        /// Set up for a call to the Stripe API
        /// </summary>
        /// <returns></returns>
        private RequestOptions GetStripeApiRequestOptions(string idempotencyKey = null)
        {
            return new RequestOptions
            {
                ApiKey = _stripePaymentSettings.GetActiveSecretKey(),
                IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? Guid.NewGuid().ToString("N") : idempotencyKey
            };
        }

        private CacheKey GetPendingPaymentCacheKey(ProcessPaymentRequest request)
        {
            return _staticCacheManager.PrepareKey(PendingPaymentCacheKey, request.CustomerId, request.OrderGuid);
        }

        private Task<StripePendingPayment> GetPendingPaymentAsync(ProcessPaymentRequest request)
        {
            return _staticCacheManager.GetAsync(GetPendingPaymentCacheKey(request), default(StripePendingPayment));
        }

        private Task SetPendingPaymentAsync(ProcessPaymentRequest request, string paymentIntentId)
        {
            return _staticCacheManager.SetAsync(GetPendingPaymentCacheKey(request), new StripePendingPayment(paymentIntentId));
        }

        private Task ClearPendingPaymentAsync(ProcessPaymentRequest request)
        {
            return _staticCacheManager.RemoveAsync(GetPendingPaymentCacheKey(request));
        }

        private static string BuildThreeDsError(PaymentIntent paymentIntent)
        {
            const string fallback = "Your bank requires additional verification. Please complete the verification in this checkout and try again.";
            if (string.IsNullOrWhiteSpace(paymentIntent?.ClientSecret))
                return fallback;
            return $"{StripeThreeDsErrorPrefix}{paymentIntent.ClientSecret}|{fallback}";
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
            var result = new ProcessPaymentResult();
            PaymentIntent paymentIntent = null;

            try
            {
                // Re-use a pending intent after the customer completes 3DS.
                var pending = await GetPendingPaymentAsync(processPaymentRequest);
                var paymentIntentService = new PaymentIntentService();
                if (pending != null && !string.IsNullOrWhiteSpace(pending.PaymentIntentId))
                {
                    paymentIntent = await paymentIntentService.GetAsync(pending.PaymentIntentId, null, GetStripeApiRequestOptions());

                    // handleCardAction completes the customer challenge first;
                    // Stripe can then leave the intent in requires_confirmation.
                    // Confirm it server-side before nopCommerce persists an order.
                    if (paymentIntent.Status == "requires_confirmation")
                    {
                        paymentIntent = await paymentIntentService.ConfirmAsync(
                            paymentIntent.Id, null,
                            GetStripeApiRequestOptions($"nop-order-confirm-{processPaymentRequest.OrderGuid:N}"));
                    }

                    if (paymentIntent.Status == "succeeded")
                    {
                        await ClearPendingPaymentAsync(processPaymentRequest);
                        return new ProcessPaymentResult
                        {
                            NewPaymentStatus = PaymentStatus.Paid,
                            AuthorizationTransactionId = paymentIntent.Id,
                            AuthorizationTransactionResult = $"Stripe PaymentIntent {paymentIntent.Id} succeeded."
                        };
                    }

                    if (paymentIntent.Status == "requires_action")
                    {
                        result.Errors = new List<string> { BuildThreeDsError(paymentIntent) };
                        return result;
                    }

                    await ClearPendingPaymentAsync(processPaymentRequest);
                    result.Errors = new List<string>
                    {
                        GetStripePaymentErrorMessage(paymentIntent.LastPaymentError?.Code,
                            paymentIntent.LastPaymentError?.DeclineCode)
                    };
                    return result;
                }

                var customer = await _paymentStripeService.GetBuyer(processPaymentRequest.CustomerId);
                if (customer == null || string.IsNullOrWhiteSpace(customer.Id))
                    throw new NopException("No valid customer was found for this payment.");

                var cart = await _shoppingCartService.GetShoppingCartAsync(customer.Customer,
                    ShoppingCartType.ShoppingCart, processPaymentRequest.StoreId);
                if (!cart.Any())
                    throw new NopException("No product was found in your cart.");

                if (customer.billingAddress == null || string.IsNullOrWhiteSpace(customer.billingAddress.Address1))
                    throw new NopException("Customer billing address not set.");

                var currency = await _workContext.GetWorkingCurrencyAsync();
                var shoppingCartTotal = await _orderTotalCalculationService.GetShoppingCartTotalAsync(cart, true);
                var total = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(
                    shoppingCartTotal.shoppingCartTotal.GetValueOrDefault(), currency);

                var paymentMethodOptions = new PaymentMethodCreateOptions
                {
                    Type = "card",
                    Card = new PaymentMethodCardOptions
                    {
                        Number = processPaymentRequest.CreditCardNumber,
                        ExpMonth = processPaymentRequest.CreditCardExpireMonth,
                        ExpYear = processPaymentRequest.CreditCardExpireYear,
                        Cvc = processPaymentRequest.CreditCardCvv2
                    },
                    BillingDetails = new PaymentMethodBillingDetailsOptions
                    {
                        Name = processPaymentRequest.CreditCardName,
                        Address = new AddressOptions
                        {
                            Line1 = customer.billingAddress.Address1,
                            Line2 = customer.billingAddress.Address2,
                            City = customer.billingAddress.City,
                            State = customer.billingAddress.State,
                            PostalCode = customer.billingAddress.ZipCode,
                            Country = customer.billingAddress.Country
                        }
                    }
                };

                var paymentMethodService = new PaymentMethodService();
                var paymentMethod = await paymentMethodService.CreateAsync(paymentMethodOptions, GetStripeApiRequestOptions());
                var stripeCustomer = await new CustomerService().CreateAsync(new CustomerCreateOptions
                {
                    Email = customer.billingAddress.Email,
                    Address = new AddressOptions
                    {
                        City = customer.billingAddress.City,
                        Country = customer.billingAddress.Country,
                        Line1 = customer.billingAddress.Address1,
                        Line2 = customer.billingAddress.Address2,
                        PostalCode = customer.billingAddress.ZipCode,
                        State = customer.billingAddress.State
                    },
                    Name = $"{customer.billingAddress.Name} {customer.billingAddress.Surname}"
                }, GetStripeApiRequestOptions());

                await paymentMethodService.AttachAsync(paymentMethod.Id,
                    new PaymentMethodAttachOptions { Customer = stripeCustomer.Id },
                    GetStripeApiRequestOptions());

                var paymentIntentOptions = new PaymentIntentCreateOptions
                {
                    Amount = (long)Math.Round(total * 100m, MidpointRounding.AwayFromZero),
                    Currency = currency.CurrencyCode.ToLowerInvariant(),
                    PaymentMethod = paymentMethod.Id,
                    Customer = stripeCustomer.Id,
                    Confirm = true,
                    Metadata = new Dictionary<string, string>
                    {
                        { "checkout_order_guid", processPaymentRequest.OrderGuid.ToString() },
                        { "payment_method_id", paymentMethod.Id }
                    },
                    AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
                    {
                        Enabled = true,
                        AllowRedirects = "never"
                    },
                    PaymentMethodOptions = new PaymentIntentPaymentMethodOptionsOptions
                    {
                        Card = new PaymentIntentPaymentMethodOptionsCardOptions
                        {
                            RequestThreeDSecure = "automatic"
                        }
                    }
                };

                if (customer.shippinAddress != null)
                {
                    paymentIntentOptions.Shipping = new ChargeShippingOptions
                    {
                        Address = new AddressOptions
                        {
                            City = customer.shippinAddress.City,
                            Country = customer.shippinAddress.Country,
                            Line1 = customer.shippinAddress.Address1,
                            Line2 = customer.shippinAddress.Address2,
                            State = customer.shippinAddress.State,
                            PostalCode = customer.shippinAddress.ZipCode
                        },
                        Phone = customer.billingAddress.GsmNumber,
                        Name = $"{customer.billingAddress.Name} {customer.billingAddress.Surname}"
                    };
                }

                var itemDescriptions = new List<string>();
                for (var i = 0; i < cart.Count; i++)
                {
                    var cartItem = cart[i];
                    var product = await _productService.GetProductByIdAsync(cartItem.ProductId);
                    var price = (await _shoppingCartService.GetUnitPriceAsync(cartItem, true)).unitPrice;
                    var productName = string.IsNullOrWhiteSpace(product.Sku) ? product.Name : $"{product.Name} ({product.Sku})";
                    var productType = product.IsShipEnabled ? "PHYSICAL - Shipping Required" : "VIRTUAL - Shipping Not Required";
                    var itemDescription = $"Unit Count:{cartItem.Quantity}; Product Name:{productName}; Price:{price}; Product Type:{productType}";
                    paymentIntentOptions.Metadata[$"Item{i + 1}"] = itemDescription.Length > 500 ? itemDescription[..500] : itemDescription;
                    itemDescriptions.Add($"{productName} x {cartItem.Quantity} ({productType})");
                }

                paymentIntentOptions.Description = string.Join(Environment.NewLine, itemDescriptions);
                paymentIntent = await paymentIntentService.CreateAsync(paymentIntentOptions,
                    GetStripeApiRequestOptions($"nop-order-{processPaymentRequest.OrderGuid:N}"));

                await _logger.InformationAsync($"Stripe PaymentIntent confirmed before order creation: ID={paymentIntent.Id}, Status={paymentIntent.Status}, Amount={paymentIntent.Amount}, Currency={paymentIntent.Currency}");

                if (paymentIntent.Status == "succeeded")
                {
                    await ClearPendingPaymentAsync(processPaymentRequest);
                    result.NewPaymentStatus = PaymentStatus.Paid;
                    result.AuthorizationTransactionId = paymentIntent.Id;
                    result.AuthorizationTransactionResult = $"Stripe PaymentIntent {paymentIntent.Id} succeeded.";
                    return result;
                }

                if (paymentIntent.Status == "requires_action")
                {
                    await SetPendingPaymentAsync(processPaymentRequest, paymentIntent.Id);
                    result.Errors = new List<string> { BuildThreeDsError(paymentIntent) };
                    return result;
                }

                await ClearPendingPaymentAsync(processPaymentRequest);
                result.Errors = new List<string>
                {
                    GetStripePaymentErrorMessage(paymentIntent.LastPaymentError?.Code,
                        paymentIntent.LastPaymentError?.DeclineCode)
                };
                return result;
            }
            catch (StripeException ex)
            {
                await _logger.WarningAsync($"Stripe payment was rejected before order creation: {ex.StripeError?.Code ?? ex.Message}");
                result.Errors = new List<string>
                {
                    GetStripePaymentErrorMessage(ex.StripeError?.Code, ex.StripeError?.DeclineCode)
                };
                return result;
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync("Stripe payment could not be prepared before order creation.", ex);
                result.Errors = new List<string>
                {
                    "We couldn't complete the card payment. Please try again or use another payment method."
                };
                return result;
            }
        }


        /// <summary>
        /// Post process payment (used by payment gateways that require redirecting to a third-party URL)
        /// </summary>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public async Task PostProcessPaymentAsync(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            var order = postProcessPaymentRequest.Order;
            try
            {
                var paymentIntent = await new PaymentIntentService().GetAsync(
                    order.AuthorizationTransactionId, null, GetStripeApiRequestOptions());

                if (!string.Equals(paymentIntent.Status, "succeeded", StringComparison.OrdinalIgnoreCase))
                {
                    var message = GetStripePaymentErrorMessage(paymentIntent.LastPaymentError?.Code,
                        paymentIntent.LastPaymentError?.DeclineCode);
                    await FailPaymentAsync(order, message);
                    throw new NopException(message);
                }

                await MarkOrderPaidAsync(order, paymentIntent);
                await EnrichStripePaymentRecordsAsync(order, paymentIntent);
            }
            catch (StripeException ex)
            {
                var message = GetStripePaymentErrorMessage(ex.StripeError?.Code, ex.StripeError?.DeclineCode);
                await FailPaymentAsync(order, message);
                throw new NopException(message);
            }
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

            var hasStripeToken = form.TryGetValue("stripeToken", out StringValues stripeToken) &&
                                 stripeToken.Count == 1 &&
                                 !string.IsNullOrWhiteSpace(stripeToken[0]) &&
                                 IsStripeTokenID(stripeToken[0]);
         
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

            // The current card form intentionally posts card fields directly. A token,
            // when supplied by an alternative Stripe.js form, must still be valid; a
            // missing token is accepted here and the card-field validator remains the
            // source of truth for the current checkout form.
            if (form.ContainsKey("stripeToken") && !hasStripeToken)
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
            // StripePaymentProcessor.cs - InstallAsync içine ekle
            await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
            {
                ["Plugins.Payments.Stripe.ProcessingHeader"] = "Your payment is being processed",
                ["Plugins.Payments.Stripe.ProcessingText"] = "Please wait while we process your payment...",
                ["Plugins.Payments.Stripe.ProcessingNotification"] = "If there are any issues, we'll contact you via email",
                ["Plugins.Payments.Stripe.StatusRequiresAction"] = "Additional verification required - please check your email",
                ["Plugins.Payments.Stripe.StatusProcessing"] = "Payment is still processing - please wait",
                ["Plugins.Payments.Stripe.StatusUnknown"] = "Payment status unknown - please contact support"
            });

            await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
            {
                ["Plugins.Payments.Stripe.EmailTemplates.PaymentActionRequired.Subject"] = "Action Required: Complete Your Payment",
                ["Plugins.Payments.Stripe.EmailTemplates.PaymentActionRequired.Body"] = @"
        Dear {{Order.CustomerFullName}},
        
        We noticed that your payment for order #{{Order.OrderNumber}} requires additional verification.
        Please follow the link below to complete your payment:
        
        {{Payment.Link}}
        
        If you have any questions, please contact us at {{Store.URL}}.
        
        Best regards,
        {{Store.Name}} Team"
            });




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
                    ["Plugins.Payments.Stripe.Fields.UseSandbox"] = "Use Stripe test mode",
                    ["Plugins.Payments.Stripe.Fields.TestPublishableKey"] = "Test publishable key",
                    ["Plugins.Payments.Stripe.Fields.TestSecretKey"] = "Test secret key",
                    ["Plugins.Payments.Stripe.Fields.TestWebhookSecret"] = "Test webhook signing secret",
                    ["Plugins.Payments.Stripe.Fields.TestWebhookEndpointId"] = "Test webhook endpoint ID",
                    ["Plugins.Payments.Stripe.Fields.TestWebhookEndpointUrl"] = "Test webhook endpoint URL",
                    ["Plugins.Payments.Stripe.Fields.WebhookSecret"] = "Webhook signing secret",
                    ["Plugins.Payments.Stripe.Fields.WebhookSecret.Hint"] = "The whsec_ signing secret shown by Stripe for this endpoint. Never use the Stripe API secret key here.",
                    ["Plugins.Payments.Stripe.Fields.WebhookEndpointId"] = "Webhook endpoint ID",
                    ["Plugins.Payments.Stripe.Fields.WebhookEndpointUrl"] = "Webhook endpoint URL",
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
                    ["Plugins.Payments.Stripe.Fields.UseSandbox"] = "Use Stripe test mode",
                    ["Plugins.Payments.Stripe.Fields.TestPublishableKey"] = "Test publishable key",
                    ["Plugins.Payments.Stripe.Fields.TestSecretKey"] = "Test secret key",
                    ["Plugins.Payments.Stripe.Fields.TestWebhookSecret"] = "Test webhook signing secret",
                    ["Plugins.Payments.Stripe.Fields.TestWebhookEndpointId"] = "Test webhook endpoint ID",
                    ["Plugins.Payments.Stripe.Fields.TestWebhookEndpointUrl"] = "Test webhook endpoint URL",
                    ["Plugins.Payments.Stripe.Fields.WebhookSecret"] = "Webhook signing secret",
                    ["Plugins.Payments.Stripe.Fields.WebhookSecret.Hint"] = "The whsec_ signing secret shown by Stripe for this endpoint. Never use the Stripe API secret key here.",
                    ["Plugins.Payments.Stripe.Fields.WebhookEndpointId"] = "Webhook endpoint ID",
                    ["Plugins.Payments.Stripe.Fields.WebhookEndpointUrl"] = "Webhook endpoint URL",
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


            // Zamanlanmış görevi oluştur veya güncelle
            if (await _scheduleTaskService.GetTaskByTypeAsync("Nop.Plugin.Payments.Stripe.Services.StripePendingPaymentTask") is null)
            {
                var scheduleTask = new ScheduleTask
                {
                    Name = "Stripe Pending Payment Check",
                    LastEnabledUtc = DateTime.UtcNow,
                    Seconds = 300, // 5 dakikada bir çalışsın
                    Type = "Nop.Plugin.Payments.Stripe.Services.StripePendingPaymentTask",
                    Enabled = true,
                    StopOnError = false
                };
                await _scheduleTaskService.InsertTaskAsync(scheduleTask);

            }

            await base.InstallAsync();
        }

        public override async Task UpdateAsync(string currentVersion, string targetVersion)
        {
            await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
            {
                ["Plugins.Payments.Stripe.Fields.UseSandbox"] = "Use Stripe test mode",
                ["Plugins.Payments.Stripe.Fields.TestPublishableKey"] = "Test publishable key",
                ["Plugins.Payments.Stripe.Fields.TestSecretKey"] = "Test secret key",
                ["Plugins.Payments.Stripe.Fields.TestWebhookSecret"] = "Test webhook signing secret",
                ["Plugins.Payments.Stripe.Fields.TestWebhookEndpointId"] = "Test webhook endpoint ID",
                ["Plugins.Payments.Stripe.Fields.TestWebhookEndpointUrl"] = "Test webhook endpoint URL",
                ["Plugins.Payments.Stripe.Fields.WebhookSecret"] = "Webhook signing secret",
                ["Plugins.Payments.Stripe.Fields.WebhookSecret.Hint"] = "The whsec_ signing secret shown by Stripe for this endpoint. Never use the Stripe API secret key here.",
                ["Plugins.Payments.Stripe.Fields.WebhookEndpointId"] = "Webhook endpoint ID",
                ["Plugins.Payments.Stripe.Fields.WebhookEndpointUrl"] = "Webhook endpoint URL"
            });

            foreach (var language in await _languageService.GetAllLanguagesAsync())
            {
                if (!string.Equals(language.UniqueSeoCode, "tr", StringComparison.OrdinalIgnoreCase))
                    continue;

                await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
                {
                    ["Plugins.Payments.Stripe.Fields.UseSandbox"] = "Stripe test modu kullan",
                    ["Plugins.Payments.Stripe.Fields.TestPublishableKey"] = "Test yayınlanabilir anahtar",
                    ["Plugins.Payments.Stripe.Fields.TestSecretKey"] = "Test gizli anahtar",
                    ["Plugins.Payments.Stripe.Fields.TestWebhookSecret"] = "Test webhook imza anahtarı",
                    ["Plugins.Payments.Stripe.Fields.TestWebhookEndpointId"] = "Test webhook endpoint kimliği",
                    ["Plugins.Payments.Stripe.Fields.TestWebhookEndpointUrl"] = "Test webhook endpoint URL’si",
                    ["Plugins.Payments.Stripe.Fields.WebhookSecret"] = "Webhook imza anahtarı",
                    ["Plugins.Payments.Stripe.Fields.WebhookSecret.Hint"] = "Bu endpoint için Stripe Dashboard’da görünen whsec_ imza anahtarıdır. Stripe API gizli anahtarını burada kullanmayın.",
                    ["Plugins.Payments.Stripe.Fields.WebhookEndpointId"] = "Webhook endpoint kimliği",
                    ["Plugins.Payments.Stripe.Fields.WebhookEndpointUrl"] = "Webhook endpoint URL’si"
                }, language.Id);
            }

            await base.UpdateAsync(currentVersion, targetVersion);
        }

        /// <summary>
        /// Uninstall the plugin
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
        public override async Task UninstallAsync()
        {
            // Zamanlanmış görevi sil
            var task = await _scheduleTaskService.GetTaskByTypeAsync("Nop.Plugin.Payments.Stripe.Services.StripePendingPaymentTask");
            if (task != null)
                await _scheduleTaskService.DeleteTaskAsync(task);
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

        public Type GetPublicViewComponent()
        {
            
            return typeof(PaymentStripeViewComponent);

        }

        // StripePaymentProcessor.cs

        public async Task ConfirmPendingPaymentIntentAsync(Order order)
        {
            if (string.IsNullOrEmpty(order.AuthorizationTransactionId))
                throw new NopException("No authorization transaction ID found");

            var service = new PaymentIntentService();
            var paymentIntent = await service.GetAsync(order.AuthorizationTransactionId, null, GetStripeApiRequestOptions());

            switch (paymentIntent.Status)
            {
                case "requires_confirmation":
                    await ConfirmPaymentIntent(paymentIntent, order);
                    break;
                case "requires_action":
                    await HandleRequiresAction(paymentIntent, order);
                    break;
                case "succeeded":
                    await UpdateOrderStatus(order, PaymentStatus.Paid, OrderStatus.Processing);
                    break;
                case "requires_payment_method":
                case "canceled":
                    await HandleUnsuccessfulPaymentIntent(paymentIntent, order);
                    break;
                default:
                    throw new NopException($"Unhandled payment status: {paymentIntent.Status}");
            }
        }
        /// <summary>
        /// Siparişe yeni bir not ekler.
        /// </summary>
        /// <param name="order">Sipariş</param>
        /// <param name="note">Eklenecek not</param>
        /// <param name="displayToCustomer">Müşteriye gösterilsin mi?</param>
        /// <returns></returns>
        private async Task CreateOrderNote(Order order, string note, bool displayToCustomer = true)
        {
            if (order == null)
                throw new ArgumentNullException(nameof(order));

            if (string.IsNullOrEmpty(note))
                throw new ArgumentNullException(nameof(note));

            // Yeni sipariş notu oluştur
            var orderNote = new OrderNote
            {
                OrderId = order.Id,
                Note = note,
                DisplayToCustomer = displayToCustomer,
                CreatedOnUtc = DateTime.UtcNow
            };

            // Sipariş notunu veritabanına ekle
            await _orderService.InsertOrderNoteAsync(orderNote);

            // Loglama yap
            await _logger.InformationAsync($"[Stripe] Order note added for order {order.CustomOrderNumber}: {note}");
        }
        /// <summary>
        /// Müşteriye e-posta gönderir.
        /// </summary>
        /// <param name="order">Sipariş</param>
        /// <param name="messageTemplateName">E-posta şablonu adı</param>
        /// <param name="tokens">E-posta içeriğinde kullanılacak token'lar</param>
        /// <returns></returns>
        /// <summary>
        /// Müşteriye e-posta gönderir.
        /// </summary>
        /// <param name="order">Sipariş</param>
        /// <param name="messageTemplateName">E-posta şablonu adı</param>
        /// <param name="tokens">E-posta içeriğinde kullanılacak token'lar</param>
        /// <returns></returns>
        private async Task SendCustomerEmail(Order order, string messageTemplateName, IEnumerable<Token> tokens = null)
        {
            if (order == null)
                throw new ArgumentNullException(nameof(order));

            if (string.IsNullOrEmpty(messageTemplateName))
                throw new ArgumentNullException(nameof(messageTemplateName));

            // Müşteri bilgilerini al
            var customer = await _customerService.GetCustomerByIdAsync(order.CustomerId);
            if (customer == null)
                throw new NopException($"Customer not found for order {order.CustomOrderNumber}");

            // Dil ve mağaza bilgilerini al
            var languageId = customer.LanguageId ?? (await _workContext.GetWorkingLanguageAsync()).Id;
            var store = await _storeContext.GetCurrentStoreAsync();

            // Token listesini hazırla (varsayılan token'ları ekle)
            var defaultTokens = new List<Nop.Services.Messages.Token>
    {
        new ("Order.CustomerFullName", customer.FirstName+" "+customer.LastName),
        new ("Order.CustomerEmail", customer.Email),
        new ("Order.OrderNumber", order.CustomOrderNumber),
        new ("Store.Name", store.Name),
        new ("Store.URL", store.Url)
    };

           

            // Sipariş token'larını ekle
            await _messageTokenProvider.AddOrderTokensAsync(defaultTokens, order, languageId);

            // E-posta şablonunu al
            var subjectTemplate = await _localizationService.GetResourceAsync($"Plugins.Payments.Stripe.EmailTemplates.{messageTemplateName}.Subject", languageId);
            var bodyTemplate = await _localizationService.GetResourceAsync($"Plugins.Payments.Stripe.EmailTemplates.{messageTemplateName}.Body", languageId);

            // Token'ları şablona uygula
            var subject = _tokenizer.Replace(subjectTemplate, defaultTokens, false);
            var body = _tokenizer.Replace(bodyTemplate, defaultTokens, true);

            // E-posta kuyruğuna ekle
            var emailAccount = await _emailAccountService.GetEmailAccountByIdAsync(_emailAccountSettings.DefaultEmailAccountId)
                ?? (await _emailAccountService.GetAllEmailAccountsAsync()).FirstOrDefault();
            if (emailAccount == null)
            {
                await _logger.WarningAsync($"Stripe email notification skipped for order {order.CustomOrderNumber}: no email account is configured.");
                return;
            }
            var email = new QueuedEmail
            {
                Priority = QueuedEmailPriority.High,
                From = emailAccount.Email,
                FromName = emailAccount.DisplayName,
                To = customer.Email,
                Subject = subject,
                Body = body,
                CreatedOnUtc = DateTime.UtcNow,
                EmailAccountId = emailAccount.Id
            };

            try
            {
                await _queuedEmailService.InsertQueuedEmailAsync(email);
            }
            catch (Exception emailException)
            {
                await _logger.ErrorAsync("Stripe customer notification could not be queued.", emailException);
            }

            // Loglama yap
            await _logger.InformationAsync($"[Stripe] Email sent to customer {customer.Email} for order {order.CustomOrderNumber} using template {messageTemplateName}");
        }

        private async Task ConfirmPaymentIntent(PaymentIntent paymentIntent, Order order)
        {
            var service = new PaymentIntentService();
            var confirmedIntent = await service.ConfirmAsync(paymentIntent.Id, null, GetStripeApiRequestOptions());

            if (confirmedIntent.Status == "succeeded")
            {
                await UpdateOrderStatus(order, PaymentStatus.Paid, OrderStatus.Processing);
                await CreateOrderNote(order, "Payment automatically confirmed by system");
            }
        }

        private async Task HandleRequiresAction(PaymentIntent paymentIntent, Order order)
        {
            await CreateOrderNote(order, "Payment requires additional action. Customer should check their email for instructions.");
            await SendCustomerEmail(order, "PaymentActionRequired");
        }

        private async Task HandleUnsuccessfulPaymentIntent(PaymentIntent paymentIntent, Order order)
        {
            // Stripe has no chargeable payment method (or the intent was cancelled).
            // Stop retrying the pending order instead of turning an expected payment
            // failure into an unhandled application error on every scheduled run.
            await UpdateOrderStatus(order, PaymentStatus.Voided, OrderStatus.Cancelled);
            await CreateOrderNote(order,
                $"Stripe payment was not completed (status: {paymentIntent.Status}). The order was cancelled; the customer can place a new order with another payment method.");
            await _logger.WarningAsync($"[Stripe] Payment intent {paymentIntent.Status}; order {order.CustomOrderNumber} cancelled without retry.");
        }

        private async Task UpdateOrderStatus(Order order, PaymentStatus paymentStatus, OrderStatus orderStatus)
        {
            order.PaymentStatus = paymentStatus;
            order.OrderStatus = orderStatus;
            await _orderService.UpdateOrderAsync(order);
        }


        // StripePaymentProcessor.cs içinde
        private async Task LogPaymentError(Order order, Exception ex, string stage)
        {
            var errorMessage = $@"Stripe Payment Error:
        Stage: {stage}
        Order: {order.CustomOrderNumber}
        Customer: {order.CustomerId}
        Error: {ex.Message}
        StackTrace: {ex.StackTrace}";

            if (ex is StripeException stripeEx)
            {
                errorMessage += $"\nStripe Error: {stripeEx.StripeError?.Code} - {stripeEx.StripeError?.Message}";
            }

            await _logger.InsertLogAsync(LogLevel.Error, "Stripe Payment Error", errorMessage);
            await SendAdminNotification($"Stripe Payment Error - {stage}", errorMessage);
        }


        private async Task SendAdminNotification(string subject, string message)
        {
            var store = await _storeContext.GetCurrentStoreAsync();
            var emailAccount = await _emailAccountService.GetEmailAccountByIdAsync(_emailAccountSettings.DefaultEmailAccountId)
                ?? (await _emailAccountService.GetAllEmailAccountsAsync()).FirstOrDefault();
            if (emailAccount == null)
            {
                await _logger.WarningAsync("Stripe admin payment notification skipped: no email account is configured.");
                return;
            }

            try
            {
                await _queuedEmailService.InsertQueuedEmailAsync(new QueuedEmail
                {
                    Priority = QueuedEmailPriority.High,
                    From = emailAccount.Email,
                    To = emailAccount.Email,
                    Subject = subject,
                    Body = message,
                    CreatedOnUtc = DateTime.UtcNow,
                    EmailAccountId = emailAccount.Id
                });
            }
            catch (Exception emailException)
            {
                // A notification must never replace the original payment error (for
                // example, a stale EmailAccount FK must not become the checkout page).
                await _logger.ErrorAsync("Stripe admin notification could not be queued.", emailException);
            }
        }

        private string GetPaymentReturnUrl()
        {
            return $"{_webHelper.GetStoreLocation().TrimEnd('/')}/checkout/OpcCompleteRedirectionPayment";
        }

        private static string GetStripePaymentErrorMessage(string code, string declineCode)
        {
            var normalizedCode = (code ?? string.Empty).Trim().ToLowerInvariant();
            var normalizedDeclineCode = (declineCode ?? string.Empty).Trim().ToLowerInvariant();

            if (normalizedCode == "insufficient_funds" || normalizedDeclineCode == "insufficient_funds" ||
                normalizedDeclineCode == "balance_insufficient")
                return "Your card does not have enough available balance. Please use another card.";

            if (normalizedCode == "card_declined" || normalizedDeclineCode is "generic_decline" or "do_not_honor" or "transaction_not_allowed")
                return "Your card was declined. Please check your card details or try another card.";

            if (normalizedCode is "expired_card" or "invalid_expiry_month" or "invalid_expiry_year")
                return "Your card has expired or its expiry date is invalid. Please check the date and try again.";

            if (normalizedCode is "incorrect_cvc" or "invalid_cvc")
                return "The card security code is incorrect. Please check it and try again.";

            if (normalizedCode is "incorrect_number" or "invalid_number")
                return "The card number is invalid. Please check it and try again.";

            if (normalizedCode is "authentication_required" or "payment_intent_authentication_failure")
                return "Your bank requires additional verification. Please try again and complete the verification step.";

            return "We couldn't complete the card payment. Please try again or use another payment method.";
        }

        private async Task MarkOrderPaidAsync(Order order, PaymentIntent paymentIntent)
        {
            order.PaymentStatus = PaymentStatus.Paid;
            order.OrderStatus = OrderStatus.Processing;
            order.AuthorizationTransactionId = paymentIntent.LatestChargeId ?? paymentIntent.Id;
            order.AuthorizationTransactionResult = $"Stripe PaymentIntent {paymentIntent.Id} succeeded.";

            await _orderService.UpdateOrderAsync(order);

            try
            {
                await _orderService.InsertOrderNoteAsync(new OrderNote
                {
                    OrderId = order.Id,
                    Note = order.AuthorizationTransactionResult,
                    DisplayToCustomer = false,
                    CreatedOnUtc = DateTime.UtcNow
                });
            }
            catch (Exception noteException)
            {
                await _logger.ErrorAsync("Stripe success order note could not be created.", noteException);
            }
        }

        private async Task EnrichStripePaymentRecordsAsync(Order order, PaymentIntent paymentIntent)
        {
            var metadata = new Dictionary<string, string>
            {
                ["order_id"] = order.Id.ToString(),
                ["order_number"] = TrimStripeValue(order.CustomOrderNumber),
                ["payment_intent_id"] = TrimStripeValue(paymentIntent.Id),
                ["currency"] = TrimStripeValue(paymentIntent.Currency),
                ["amount_minor"] = paymentIntent.Amount.ToString(),
                ["shipping_method"] = TrimStripeValue(order.ShippingMethod)
            };

            var orderItems = await _orderService.GetOrderItemsAsync(order.Id);
            var itemSummary = new List<string>();
            var statusSummary = new List<string>();
            var productionSummary = new List<string>();
            foreach (var item in orderItems)
            {
                var product = await _productService.GetProductByIdAsync(item.ProductId);
                if (product == null)
                    continue;
                var productName = string.IsNullOrWhiteSpace(product.Sku) ? product.Name : product.Sku;
                var status = product.IsShipEnabled ? "physical" : "virtual";
                var production = await TryGetProductionMetadataAsync(product.Id);
                if (!string.IsNullOrWhiteSpace(production.Status))
                    status = production.Status;
                if (!string.IsNullOrWhiteSpace(production.Production))
                    productionSummary.Add($"{productName}: {production.Production}");
                statusSummary.Add($"{productName}: {status}");
                itemSummary.Add($"{productName} x {item.Quantity}");
            }

            metadata["items"] = TrimStripeValue(string.Join(" | ", itemSummary));
            metadata["product_status"] = TrimStripeValue(string.Join(" | ", statusSummary));
            metadata["production_time"] = TrimStripeValue(string.Join(" | ", productionSummary));

            try
            {
                var paymentIntentService = new PaymentIntentService();
                await paymentIntentService.UpdateAsync(paymentIntent.Id, new PaymentIntentUpdateOptions
                {
                    Description = $"Order {order.CustomOrderNumber}: {string.Join(" | ", itemSummary)}",
                    Metadata = metadata
                }, GetStripeApiRequestOptions($"nop-payment-intent-order-{order.Id}"));

                if (!string.IsNullOrWhiteSpace(paymentIntent.LatestChargeId))
                {
                    await new ChargeService().UpdateAsync(paymentIntent.LatestChargeId, new ChargeUpdateOptions
                    {
                        Description = $"Order {order.CustomOrderNumber}",
                        Metadata = metadata
                    }, GetStripeApiRequestOptions($"nop-charge-order-{order.Id}"));
                }

                if (!string.IsNullOrWhiteSpace(paymentIntent.CustomerId))
                {
                    var invoiceService = new InvoiceService();
                    var invoiceDetail = $"Items: {string.Join(" | ", itemSummary)}; Status: {metadata["product_status"]}; Production: {metadata["production_time"]}";
                    var invoice = await invoiceService.CreateAsync(new InvoiceCreateOptions
                    {
                        Customer = paymentIntent.CustomerId,
                        Currency = paymentIntent.Currency,
                        CollectionMethod = "send_invoice",
                        AutoAdvance = false,
                        Description = TrimStripeValue($"Hood Archery Shop order {order.CustomOrderNumber}; {invoiceDetail}"),
                        Metadata = metadata
                    }, GetStripeApiRequestOptions($"nop-invoice-order-{order.Id}"));

                    await new InvoiceItemService().CreateAsync(new InvoiceItemCreateOptions
                    {
                        Customer = paymentIntent.CustomerId,
                        Invoice = invoice.Id,
                        Amount = paymentIntent.Amount,
                        Currency = paymentIntent.Currency,
                        Description = TrimStripeValue(invoiceDetail),
                        Metadata = new Dictionary<string, string>(metadata)
                    }, GetStripeApiRequestOptions($"nop-invoice-item-order-{order.Id}"));

                    var finalizedInvoice = invoice.Status == "draft"
                        ? await invoiceService.FinalizeInvoiceAsync(invoice.Id, new InvoiceFinalizeOptions { AutoAdvance = false },
                            GetStripeApiRequestOptions($"nop-invoice-finalize-order-{order.Id}"))
                        : invoice;
                    if (!string.Equals(finalizedInvoice.Status, "paid", StringComparison.OrdinalIgnoreCase))
                    {
                        finalizedInvoice = await invoiceService.PayAsync(finalizedInvoice.Id,
                            new InvoicePayOptions { PaidOutOfBand = true },
                            GetStripeApiRequestOptions($"nop-invoice-pay-order-{order.Id}"));
                    }

                    metadata["stripe_invoice_id"] = finalizedInvoice.Id;
                    await paymentIntentService.UpdateAsync(paymentIntent.Id,
                        new PaymentIntentUpdateOptions { Metadata = metadata },
                        GetStripeApiRequestOptions($"nop-payment-intent-invoice-{order.Id}"));
                    await _logger.InformationAsync($"Stripe invoice linked to order {order.CustomOrderNumber}: {finalizedInvoice.Id}");
                }
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync($"Stripe order metadata/invoice enrichment failed for order {order.CustomOrderNumber}.", ex);
                await _orderService.InsertOrderNoteAsync(new OrderNote
                {
                    OrderId = order.Id,
                    Note = "Stripe payment succeeded, but invoice metadata could not be synchronized.",
                    DisplayToCustomer = false,
                    CreatedOnUtc = DateTime.UtcNow
                });
            }
        }

        private async Task<(string Status, string Production)> TryGetProductionMetadataAsync(int productId)
        {
            try
            {
                var serviceType = Type.GetType(
                    "Nop.Plugin.Shipping.FixedByWeightByTotal.Services.ProductionTime.IProductProductionTimeService, Nop.Plugin.Shipping.FixedByWeightByTotal")
                    ?? Type.GetType(
                    "Nop.Plugin.Shipping.FixedByWeightByTotal.Services.ProductionTime.IProductProductionTimeService, Shipping.FixedByWeightByTotal");
                var service = serviceType == null ? null : _httpContextAccessor.HttpContext?.RequestServices.GetService(serviceType);
                var method = serviceType?.GetMethod("GetModelByProductIdAsync");
                if (service == null || method == null || method.Invoke(service, new object[] { productId }) is not Task task)
                    return (string.Empty, string.Empty);
                await task;
                var model = task.GetType().GetProperty("Result")?.GetValue(task);
                if (model == null)
                    return (string.Empty, string.Empty);
                var isHandmade = (bool?)model.GetType().GetProperty("IsHandmade")?.GetValue(model) == true;
                var isOrderable = (bool?)model.GetType().GetProperty("IsOrderable")?.GetValue(model) == true;
                var status = isHandmade ? (isOrderable ? "handmade-made-to-order" : "handmade") : "standard";
                var production = model.GetType().GetProperty("EffectiveProductionText")?.GetValue(model)?.ToString();
                return (status, production ?? string.Empty);
            }
            catch
            {
                return (string.Empty, string.Empty);
            }
        }

        private static string TrimStripeValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            value = value.Replace("\r", " ").Replace("\n", " ").Trim();
            return value.Length > 500 ? value[..500] : value;
        }

        private async Task FailPaymentAsync(Order order, string message)
        {
            order.PaymentStatus = PaymentStatus.Voided;
            order.OrderStatus = OrderStatus.Cancelled;
            await _orderService.UpdateOrderAsync(order);
            try
            {
                await _orderService.InsertOrderNoteAsync(new OrderNote
                {
                    OrderId = order.Id,
                    Note = $"Stripe payment was not completed: {message}",
                    DisplayToCustomer = false,
                    CreatedOnUtc = DateTime.UtcNow
                });
            }
            catch (Exception noteException)
            {
                await _logger.ErrorAsync("Stripe failure order note could not be created.", noteException);
            }
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
        // The intent is confirmed in ProcessPaymentAsync before nopCommerce
        // persists the order, so the normal one-page checkout can run
        // PostProcessPaymentAsync inline instead of redirecting to
        // OpcCompleteRedirectionPayment.
        public PaymentMethodType PaymentMethodType => PaymentMethodType.Standard;

        /// <summary>
        /// Gets a value indicating whether we should display a payment information page for this plugin
        /// </summary>
        public bool SkipPaymentInfo => false;

        #endregion
    }
}
