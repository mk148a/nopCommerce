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
            ITokenizer tokenizer

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
            var result = new ProcessPaymentResult();

            try
            {
                var customer = await _paymentStripeService.GetBuyer(processPaymentRequest.CustomerId);

                if (customer == null || string.IsNullOrEmpty( customer.Id))
                {
                    throw new Exception("No Valid Customer Found!");
                }

                var cart = await _shoppingCartService.GetShoppingCartAsync(customer.Customer, ShoppingCartType.ShoppingCart, processPaymentRequest.StoreId);
                if (!cart.Any())
                {
                    throw new Exception("No Product Found in Your Cart!");
                }

                if (string.IsNullOrEmpty(customer.billingAddress.Address1))
                {
                    throw new NopException("Customer billing address not set!");
                }

                var currency = await _workContext.GetWorkingCurrencyAsync();
                var shoppingCartTotal = await _orderTotalCalculationService.GetShoppingCartTotalAsync(cart, true);
                var shoppingCartUnitPriceWithDiscount = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(shoppingCartTotal.shoppingCartTotal.Value, currency);

                var paymentMethodOptions = new PaymentMethodCreateOptions()
                {
                    Type = "card",
                    Card = new PaymentMethodCardOptions
                    {
                        Number = processPaymentRequest.CreditCardNumber,
                        ExpMonth = processPaymentRequest.CreditCardExpireMonth,
                        ExpYear = processPaymentRequest.CreditCardExpireYear,
                        Cvc = processPaymentRequest.CreditCardCvv2,
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
                            Country = customer.billingAddress.Country,
                        }
                    }
                };
                string orderId = "";
                var order = await _orderService.GetOrderByGuidAsync(processPaymentRequest.OrderGuid);

                var paymentMethodService = new PaymentMethodService();
                var paymentMethod = await paymentMethodService.CreateAsync(paymentMethodOptions, GetStripeApiRequestOptions());

                var customerOptions = new CustomerCreateOptions
                {
                    Email = customer.billingAddress.Email,
                    Address = new AddressOptions()
                    {
                        City = customer.billingAddress.City,
                        Country = customer.billingAddress.Country,
                        Line1 = customer.billingAddress.Address1,
                        Line2 = customer.billingAddress.Address2,
                        PostalCode = customer.billingAddress.ZipCode,
                        State = customer.billingAddress.State
                    },
                    Name = customer.billingAddress.Name + " " + customer.billingAddress.Surname,
                };
                var customerService = new CustomerService();
                var stripeCustomer = await customerService.CreateAsync(customerOptions, GetStripeApiRequestOptions());

                var paymentMethodAttachOptions = new PaymentMethodAttachOptions
                {
                    Customer = stripeCustomer.Id
                };
               var attachResult= await paymentMethodService.AttachAsync(paymentMethod.Id, paymentMethodAttachOptions, GetStripeApiRequestOptions());
               if (attachResult.StripeResponse.StatusCode != HttpStatusCode.OK)
               {
                   // Hata durumunu logla ve kullanıcıya anlamlı mesaj göster
                   await _logger.ErrorAsync($"PaymentAttach oluşturulurken hata: ID={attachResult.Id}, Status={attachResult.StripeResponse.StatusCode}, Error={attachResult.StripeResponse?.Content}");
               



                    string errorMessage = attachResult.StripeResponse.Content;
                   

                   // Sipariş durumunu Cancelled olarak güncelle
                   if (order != null)
                   {
                       order.OrderStatus = OrderStatus.Cancelled;
                       await _orderService.UpdateOrderAsync(order);
                   }

                   // Hata mesajını ProcessPaymentResult ile döndür
                   result.Errors = new List<string> { errorMessage };
                   
                   return result;
                }

              
                if (order != null)
                {
                    orderId = order.Id.ToString();
                }
                else
                {
                    orderId = processPaymentRequest.OrderGuid.ToString();
                }

                var paymentIntentOptions = new PaymentIntentCreateOptions
                {
                    Amount = (long)(shoppingCartUnitPriceWithDiscount * 100),
                    Currency = currency.CurrencyCode.ToLower(),
                    PaymentMethod = paymentMethod.Id,
                    Customer = stripeCustomer.Id,
                    Confirm = false,
                    Metadata = new Dictionary<string, string>
            {
                { "order_id", orderId },
                { "payment_method_id", paymentMethod.Id }
            },
                    AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
                    {
                        Enabled = true,
                        // Stripe may send the customer to their bank for SCA/3DS.
                        AllowRedirects = "always"
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
                    paymentIntentOptions.Shipping = new ChargeShippingOptions()
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
                        Name = customer.billingAddress.Name + ' ' + customer.billingAddress.Surname
                    };
                }

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
                    paymentIntentOptions.Metadata.Add("Item" + (i + 1), "Unit Count:" + cartItem.Quantity + ";" + "Product Name:" + productName + ";" + "Price:" + price + ";" + "Product Type:" + productType);
                }

                paymentIntentOptions.Description = orderItems;

                var paymentIntentService = new PaymentIntentService();
                var paymentIntent = await paymentIntentService.CreateAsync(paymentIntentOptions, GetStripeApiRequestOptions());

                // Log PaymentIntent Oluşturma Sonucu
                await _logger.InformationAsync($"PaymentIntent oluşturuldu: ID={paymentIntent.Id}, Status={paymentIntent.Status}, Amount={paymentIntent.Amount}, Currency={paymentIntent.Currency}, OrderID={orderId}");

                if (paymentIntent.Status == "succeeded" )
                {
                    // Ödeme başarılı
                    result.NewPaymentStatus = PaymentStatus.Paid;
                    result.AuthorizationTransactionId = paymentIntent.Id;
                    result.AuthorizationTransactionResult = $"Transaction was processed by using {paymentIntent.LatestCharge?.Source.Object}. Status is {paymentIntent.Status}";
                    return result;
                }
                else if (paymentIntent.Status == "requires_confirmation")
                {
                    // Ödeme onay bekliyor
                    result.NewPaymentStatus = PaymentStatus.Pending;
                    result.AuthorizationTransactionId = paymentIntent.Id;
                    result.AuthorizationTransactionResult = $"Transaction was processed by using {paymentIntent.LatestCharge?.Source.Object}. Status is {paymentIntent.Status}";
                    return result;
                }
                else if ( paymentIntent.Status == "requires_action")
                {
                    // Ödeme ek doğrulama gerektiriyor
                    result.NewPaymentStatus = PaymentStatus.Pending;
                    result.AuthorizationTransactionId = paymentIntent.Id;
                    result.AuthorizationTransactionResult = "You must complete additional verification steps to complete your payment.";
                    result.Errors = new List<string>
                    {
                        "Your bank requires an additional verification step. Please complete the verification and try again."
                    };
                    return result;
                }
                else
                {
                    // Hata durumunu logla ve kullanıcıya anlamlı mesaj göster
                    await _logger.ErrorAsync($"PaymentIntent oluşturulurken hata: ID={paymentIntent.Id}, Status={paymentIntent.Status}, Error={paymentIntent.LastPaymentError?.Message}");

                    string errorMessage = GetStripePaymentErrorMessage(paymentIntent.LastPaymentError?.Code,
                        paymentIntent.LastPaymentError?.DeclineCode);

                    // Sipariş durumunu Cancelled olarak güncelle
                    if (order != null)
                    {
                        order.OrderStatus = OrderStatus.Cancelled;
                        await _orderService.UpdateOrderAsync(order);
                    }

                    // Hata mesajını ProcessPaymentResult ile döndür
                    result.Errors = new List<string> { errorMessage };
                    return result;
                }
            }
            catch (StripeException ex)
            {
                // Stripe spesifik hataları logla ve kullanıcıya anlamlı mesaj döndür
                await _logger.ErrorAsync($"StripeException: {ex.Message}, StripeResponse: {ex.StripeResponse?.Content}");
              
                // Sipariş durumunu Cancelled olarak güncelle
                var order = await _orderService.GetOrderByGuidAsync(processPaymentRequest.OrderGuid);
                if (order != null)
                {
                    await LogPaymentError(order, ex, "ProcesPayment");

                    order.OrderStatus = OrderStatus.Cancelled;
                    await _orderService.UpdateOrderAsync(order);
                }

                result.Errors = new List<string> { GetStripePaymentErrorMessage(ex.StripeError?.Code, ex.StripeError?.DeclineCode) };
              
               

                // Sipariş durumunu Cancelled olarak güncelle
                if (order != null)
                {
                    order.OrderStatus = OrderStatus.Cancelled;
                    await _orderService.UpdateOrderAsync(order);
                }


                return result;
            }
            catch (Exception ex)
            {
                // Genel hataları logla ve kullanıcıya anlamlı mesaj döndür
                await _logger.ErrorAsync($"Exception in ProcessPaymentAsync: {ex.Message}");

                // Sipariş durumunu Cancelled olarak güncelle
                var order = await _orderService.GetOrderByGuidAsync(processPaymentRequest.OrderGuid);

                if (order != null)
                {
                    await LogPaymentError(order, ex, "ProcesPayment");

                    order.OrderStatus = OrderStatus.Cancelled;
                    await _orderService.UpdateOrderAsync(order);
                }
                result.Errors = new List<string> { "We couldn't complete the card payment. Please try again or use another payment method." };



                // Sipariş durumunu Cancelled olarak güncelle
                if (order != null)
                {
                    order.OrderStatus = OrderStatus.Cancelled;
                    await _orderService.UpdateOrderAsync(order);
                }
               // result.Errors = new List<string> { "An error occurred during payment. Please try again." };
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
            try
            {
                var order = postProcessPaymentRequest.Order;
                var service = new PaymentIntentService();
                var paymentIntent = await service.GetAsync(order.AuthorizationTransactionId, null, GetStripeApiRequestOptions());

                // A webhook or a return from a 3DS challenge may have completed the
                // intent already. Never confirm a succeeded intent a second time.
                if (paymentIntent.Status == "succeeded")
                {
                    await MarkOrderPaidAsync(order, paymentIntent);
                    return;
                }

                if (paymentIntent.Status == "processing")
                {
                    order.PaymentStatus = PaymentStatus.Pending;
                    await _orderService.UpdateOrderAsync(order);
                    await _logger.InformationAsync($"Stripe PaymentIntent is processing: ID={paymentIntent.Id}");
                    return;
                }

                if (string.IsNullOrEmpty(paymentIntent.PaymentMethodId))
                {
                    if (paymentIntent.Metadata.TryGetValue("payment_method_id", out var paymentMethodId) &&
                        !string.IsNullOrEmpty(paymentMethodId))
                    {
                        await service.UpdateAsync(paymentIntent.Id, new PaymentIntentUpdateOptions
                        {
                            PaymentMethod = paymentMethodId
                        }, GetStripeApiRequestOptions());

                        paymentIntent = await service.GetAsync(order.AuthorizationTransactionId, null, GetStripeApiRequestOptions());
                    }
                    else
                    {
                        await _logger.ErrorAsync($"PaymentMethodId metadata missing: PaymentIntentID={paymentIntent.Id}");
                        await FailPaymentAsync(order, "We couldn't prepare your card payment. Please try again.");
                        throw new NopException("We couldn't prepare your card payment. Please try again.");
                    }
                }

                await service.UpdateAsync(paymentIntent.Id, new PaymentIntentUpdateOptions
                {
                    Description = "Order Number:" + order.Id + Environment.NewLine + paymentIntent.Description,
                    Metadata = new Dictionary<string, string> { { "order_id", order.Id.ToString() } }
                }, GetStripeApiRequestOptions());

                var confirmResult = await service.ConfirmAsync(paymentIntent.Id, new PaymentIntentConfirmOptions
                {
                    PaymentMethod = paymentIntent.PaymentMethodId,
                    ReturnUrl = GetPaymentReturnUrl(),
                    PaymentMethodOptions = new PaymentIntentPaymentMethodOptionsOptions
                    {
                        Card = new PaymentIntentPaymentMethodOptionsCardOptions
                        {
                            RequestThreeDSecure = "automatic"
                        }
                    }
                }, GetStripeApiRequestOptions());

                await _logger.InformationAsync($"Stripe PaymentIntent confirmation: ID={confirmResult.Id}, Status={confirmResult.Status}, LatestChargeID={confirmResult.LatestChargeId}");

                if (confirmResult.Status == "succeeded")
                {
                    await MarkOrderPaidAsync(order, confirmResult);
                    return;
                }

                if (confirmResult.Status == "processing")
                {
                    order.PaymentStatus = PaymentStatus.Pending;
                    await _orderService.UpdateOrderAsync(order);
                    return;
                }

                if (confirmResult.Status == "requires_action")
                {
                    var redirectUrl = confirmResult.NextAction?.RedirectToUrl?.Url;
                    if (string.Equals(confirmResult.NextAction?.Type, "redirect_to_url", StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace(redirectUrl))
                    {
                        // Stripe-hosted 3DS. The return URL re-enters this action, which
                        // re-reads the intent and marks the order paid only after succeeded.
                        _httpContextAccessor.HttpContext?.Response.Redirect(redirectUrl);
                        return;
                    }

                    await FailPaymentAsync(order,
                        "Your bank requires an additional verification step, but the verification could not be started. Please try another card.");
                    throw new NopException("Your bank requires an additional verification step, but the verification could not be started. Please try another card.");
                }

                var paymentError = GetStripePaymentErrorMessage(confirmResult.LastPaymentError?.Code,
                    confirmResult.LastPaymentError?.DeclineCode);
                await _logger.ErrorAsync($"Stripe PaymentIntent confirmation did not succeed: ID={confirmResult.Id}, Status={confirmResult.Status}");
                await FailPaymentAsync(order, paymentError);
                throw new NopException(paymentError);
            }
            catch (StripeException ex)
            {
                await _logger.ErrorAsync($"StripeException in PostProcessPaymentAsync: {ex.Message}, StripeResponse: {ex.StripeResponse?.Content}");

                var order = await _orderService.GetOrderByGuidAsync(postProcessPaymentRequest.Order.OrderGuid);
                var message = GetStripePaymentErrorMessage(ex.StripeError?.Code, ex.StripeError?.DeclineCode);
                if (order != null)
                {
                    await LogPaymentError(order, ex, "PostProcessPayment");
                    await FailPaymentAsync(order, message);
                }

                throw new NopException(message);
            }
            catch (NopException)
            {
                throw;
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync($"Exception in PostProcessPaymentAsync: {ex.Message}");

                var order = await _orderService.GetOrderByGuidAsync(postProcessPaymentRequest.Order.OrderGuid);
                if (order != null)
                {
                    await LogPaymentError(order, ex, "PostProcessPayment");
                    await FailPaymentAsync(order, "We couldn't complete the card payment. Please try again or use another payment method.");
                }

                throw new NopException("We couldn't complete the card payment. Please try again or use another payment method.");
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
        public PaymentMethodType PaymentMethodType { get; set; } = PaymentMethodType.Redirection;

        /// <summary>
        /// Gets a value indicating whether we should display a payment information page for this plugin
        /// </summary>
        public bool SkipPaymentInfo => false;

        #endregion
    }
}
