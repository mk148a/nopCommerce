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
using Nop.Plugin.Payments.StripeApplePay.Validators;
using Nop.Plugin.Payments.StripeApplePay.Models;
using Autofac.Core;
using Microsoft.Extensions.Options;
using Nop.Plugin.Payments.StripeApplePay.Components;
using Nop.Core.Caching;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using Order = Nop.Core.Domain.Orders.Order;

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
        private readonly IStaticCacheManager _staticCacheManager;

        private const string StripeThreeDsErrorPrefix = "__STRIPE_3DS__:";
        private static readonly CacheKey PendingPaymentCacheKey = new(
            "Nop.Plugin.Payments.StripeApplePay.PendingPayment.{0}.{1}",
            "Nop.Plugin.Payments.StripeApplePay.PendingPayment") { CacheTime = 15 };

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
            ILocalizationService localizationService, IAddressService addressService, ICountryService countryService,
            IStaticCacheManager staticCacheManager)
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
            _staticCacheManager = staticCacheManager;
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
        {
            var result = new ProcessPaymentResult();

            try
            {
                var pending = await GetPendingPaymentAsync(processPaymentRequest);
                var paymentIntentService = new PaymentIntentService();
                if (pending != null && !string.IsNullOrWhiteSpace(pending.PaymentIntentId))
                {
                    var pendingIntent = await paymentIntentService.GetAsync(pending.PaymentIntentId, null, GetStripeApiRequestOptions());

                    // A successful wallet 3DS challenge may leave the intent
                    // awaiting confirmation. Confirm it on the server before
                    // allowing nopCommerce to create the order.
                    if (pendingIntent.Status == "requires_confirmation")
                    {
                        pendingIntent = await paymentIntentService.ConfirmAsync(
                            pendingIntent.Id, null,
                            GetStripeApiRequestOptions($"nop-wallet-order-confirm-{processPaymentRequest.OrderGuid:N}"));
                    }

                    if (pendingIntent.Status == "succeeded")
                    {
                        await ClearPendingPaymentAsync(processPaymentRequest);
                        return new ProcessPaymentResult
                        {
                            NewPaymentStatus = PaymentStatus.Paid,
                            AuthorizationTransactionId = pendingIntent.Id,
                            AuthorizationTransactionResult = $"Stripe wallet PaymentIntent {pendingIntent.Id} succeeded."
                        };
                    }

                    if (pendingIntent.Status == "requires_action")
                    {
                        result.Errors = new List<string> { BuildThreeDsError(pendingIntent) };
                        return result;
                    }

                    await ClearPendingPaymentAsync(processPaymentRequest);
                    result.Errors = new List<string>
                    {
                        GetStripePaymentErrorMessage(pendingIntent.LastPaymentError?.Code,
                            pendingIntent.LastPaymentError?.DeclineCode)
                    };
                    return result;
                }

                if (!processPaymentRequest.CustomValues.TryGetValue("PaymentMethodId", out var paymentMethodValue) ||
                    string.IsNullOrWhiteSpace(paymentMethodValue?.ToString()))
                {
                    result.Errors = new List<string>
                    {
                        "Please select and complete Apple Pay or Google Pay before confirming your order."
                    };
                    return result;
                }

                var paymentMethodId = paymentMethodValue.ToString();
                var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
                var stripePaymentSettings = await _settingService.LoadSettingAsync<StripeApplePayPaymentSettings>(storeScope);
                StripeConfiguration.ApiKey = stripePaymentSettings.SecretKey;

                var currentCustomer = await _workContext.GetCurrentCustomerAsync();
                var currency = await _workContext.GetWorkingCurrencyAsync();
                var cart = await _shoppingCartService.GetShoppingCartAsync(currentCustomer, ShoppingCartType.ShoppingCart);
                if (!cart.Any())
                {
                    result.Errors = new List<string> { "No product was found in your cart." };
                    return result;
                }

                var shoppingCartTotal = await _orderTotalCalculationService.GetShoppingCartTotalAsync(cart, true);
                var total = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(
                    shoppingCartTotal.shoppingCartTotal.GetValueOrDefault(), currency);
                if (await _addressService.GetAddressByIdAsync(currentCustomer.BillingAddressId ?? 0) == null)
                {
                    result.Errors = new List<string> { "Customer billing address not set." };
                    return result;
                }

                // A wallet PaymentMethod is a one-time token. Attach it to a
                // Stripe Customer before confirmation so the successful
                // PaymentIntent can be represented by a paid invoice without
                // changing the nopCommerce order lifecycle.
                var buyer = await _paymentStripeService.GetBuyer(currentCustomer.Id);
                if (buyer?.billingAddress == null)
                {
                    result.Errors = new List<string> { "Customer billing address not set." };
                    return result;
                }

                var stripeCustomer = await new Stripe.CustomerService().CreateAsync(new CustomerCreateOptions
                {
                    Email = buyer.billingAddress.Email,
                    Name = $"{buyer.billingAddress.Name} {buyer.billingAddress.Surname}".Trim(),
                    Address = new AddressOptions
                    {
                        Line1 = buyer.billingAddress.Address1,
                        Line2 = buyer.billingAddress.Address2,
                        City = buyer.billingAddress.City,
                        State = buyer.billingAddress.State,
                        PostalCode = buyer.billingAddress.ZipCode,
                        Country = buyer.billingAddress.Country
                    },
                    Metadata = new Dictionary<string, string>
                    {
                        ["nop_customer_id"] = currentCustomer.Id.ToString()
                    }
                }, GetStripeApiRequestOptions($"nop-wallet-customer-{processPaymentRequest.OrderGuid:N}"));

                await new Stripe.PaymentMethodService().AttachAsync(paymentMethodId,
                    new PaymentMethodAttachOptions { Customer = stripeCustomer.Id },
                    GetStripeApiRequestOptions($"nop-wallet-payment-method-{processPaymentRequest.OrderGuid:N}"));

                var paymentIntentOptions = new PaymentIntentCreateOptions
                {
                    Amount = (long)Math.Round(total * 100m, MidpointRounding.AwayFromZero),
                    Currency = currency.CurrencyCode.ToLowerInvariant(),
                    PaymentMethodTypes = new List<string> { "card" },
                    PaymentMethod = paymentMethodId,
                    Customer = stripeCustomer.Id,
                    Confirm = true,
                    Metadata = new Dictionary<string, string>
                    {
                        ["checkout_order_guid"] = processPaymentRequest.OrderGuid.ToString(),
                        ["payment_method_id"] = paymentMethodId,
                        ["stripe_customer_id"] = stripeCustomer.Id,
                        ["nop_customer_id"] = currentCustomer.Id.ToString()
                    },
                    PaymentMethodOptions = new PaymentIntentPaymentMethodOptionsOptions
                    {
                        Card = new PaymentIntentPaymentMethodOptionsCardOptions
                        {
                            RequestThreeDSecure = "automatic"
                        }
                    }
                };

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
                var paymentIntent = await paymentIntentService.CreateAsync(paymentIntentOptions,
                    GetStripeApiRequestOptions($"nop-wallet-order-{processPaymentRequest.OrderGuid:N}"));

                if (paymentIntent.Status == "succeeded")
                {
                    await ClearPendingPaymentAsync(processPaymentRequest);
                    result.NewPaymentStatus = PaymentStatus.Paid;
                    result.AuthorizationTransactionId = paymentIntent.Id;
                    result.AuthorizationTransactionResult = $"Stripe wallet PaymentIntent {paymentIntent.Id} succeeded.";
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
                result.Errors = new List<string>
                {
                    GetStripePaymentErrorMessage(ex.StripeError?.Code, ex.StripeError?.DeclineCode)
                };
                return result;
            }
            catch
            {
                result.Errors = new List<string>
                {
                    "We couldn't complete the wallet payment. Please try again or use another payment method."
                };
                return result;
            }
        }

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
                    order.PaymentStatus = PaymentStatus.Voided;
                    order.OrderStatus = OrderStatus.Cancelled;
                    await _orderService.UpdateOrderAsync(order);
                    throw new NopException(message);
                }

                await MarkWalletOrderPaidAsync(order, paymentIntent);
                await EnrichStripePaymentRecordsAsync(order, paymentIntent);
            }
            catch (StripeException ex)
            {
                var message = GetStripePaymentErrorMessage(ex.StripeError?.Code, ex.StripeError?.DeclineCode);
                order.PaymentStatus = PaymentStatus.Voided;
                order.OrderStatus = OrderStatus.Cancelled;
                await _orderService.UpdateOrderAsync(order);
                throw new NopException(message);
            }
        }


        private async Task MarkWalletOrderPaidAsync(Order order, PaymentIntent paymentIntent)
        {
            order.OrderStatus = OrderStatus.Processing;
            order.PaymentStatus = PaymentStatus.Paid;
            order.AuthorizationTransactionId = paymentIntent.LatestChargeId ?? paymentIntent.Id;
            order.AuthorizationTransactionResult = $"Stripe wallet PaymentIntent {paymentIntent.Id} succeeded.";
            await _orderService.UpdateOrderAsync(order);
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
                }, GetStripeApiRequestOptions($"nop-wallet-payment-intent-order-{order.Id}"));

                if (!string.IsNullOrWhiteSpace(paymentIntent.LatestChargeId))
                {
                    await new ChargeService().UpdateAsync(paymentIntent.LatestChargeId, new ChargeUpdateOptions
                    {
                        Description = $"Order {order.CustomOrderNumber}",
                        Metadata = metadata
                    }, GetStripeApiRequestOptions($"nop-wallet-charge-order-{order.Id}"));
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
                    }, GetStripeApiRequestOptions($"nop-wallet-invoice-order-{order.Id}"));

                    await new InvoiceItemService().CreateAsync(new InvoiceItemCreateOptions
                    {
                        Customer = paymentIntent.CustomerId,
                        Invoice = invoice.Id,
                        Amount = paymentIntent.Amount,
                        Currency = paymentIntent.Currency,
                        Description = TrimStripeValue(invoiceDetail),
                        Metadata = new Dictionary<string, string>(metadata)
                    }, GetStripeApiRequestOptions($"nop-wallet-invoice-item-order-{order.Id}"));

                    var finalizedInvoice = invoice.Status == "draft"
                        ? await invoiceService.FinalizeInvoiceAsync(invoice.Id, new InvoiceFinalizeOptions { AutoAdvance = false },
                            GetStripeApiRequestOptions($"nop-wallet-invoice-finalize-order-{order.Id}"))
                        : invoice;
                    if (!string.Equals(finalizedInvoice.Status, "paid", StringComparison.OrdinalIgnoreCase))
                    {
                        finalizedInvoice = await invoiceService.PayAsync(finalizedInvoice.Id,
                            new InvoicePayOptions { PaidOutOfBand = true },
                            GetStripeApiRequestOptions($"nop-wallet-invoice-pay-order-{order.Id}"));
                    }
                    metadata["stripe_invoice_id"] = finalizedInvoice.Id;
                    await paymentIntentService.UpdateAsync(paymentIntent.Id, new PaymentIntentUpdateOptions { Metadata = metadata },
                        GetStripeApiRequestOptions($"nop-wallet-payment-intent-invoice-{order.Id}"));
                }
            }
            catch
            {
                // Invoice enrichment is a separate side effect after the card
                // authorization and order have succeeded.
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

        private static string GetStripePaymentErrorMessage(string code, string declineCode)
        {
            var normalizedCode = (code ?? string.Empty).Trim().ToLowerInvariant();
            var normalizedDeclineCode = (declineCode ?? string.Empty).Trim().ToLowerInvariant();
            if (normalizedCode == "insufficient_funds" || normalizedDeclineCode == "insufficient_funds" || normalizedDeclineCode == "balance_insufficient")
                return "Your card does not have enough available balance. Please use another card.";
            if (normalizedCode == "card_declined" || normalizedDeclineCode is "generic_decline" or "do_not_honor" or "transaction_not_allowed")
                return "Your card was declined. Please check your card details or try another card.";
            if (normalizedCode is "expired_card" or "invalid_expiry_month" or "invalid_expiry_year")
                return "Your card has expired or its expiry date is invalid. Please check the date and try again.";
            if (normalizedCode is "incorrect_cvc" or "invalid_cvc")
                return "The card security code is incorrect. Please check it and try again.";
            if (normalizedCode is "incorrect_number" or "invalid_number")
                return "The card number is invalid. Please check it and try again.";
            return "We couldn't complete the wallet payment. Please try again or use another payment method.";
        }

        private RequestOptions GetStripeApiRequestOptions(string idempotencyKey = null)
        {
            return new RequestOptions
            {
                ApiKey = _stripePaymentSettings.SecretKey,
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
            return string.IsNullOrWhiteSpace(paymentIntent?.ClientSecret)
                ? fallback
                : $"{StripeThreeDsErrorPrefix}{paymentIntent.ClientSecret}|{fallback}";
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
