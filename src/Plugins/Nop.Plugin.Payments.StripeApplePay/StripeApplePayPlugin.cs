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
       IGenericAttributeService genericAttributeService)
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
            var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var stripePaymentSettings = await _settingService.LoadSettingAsync<StripeApplePayPaymentSettings>(storeScope);

            var paymentMethodId = (await _genericAttributeService.GetAttributesForEntityAsync((await _workContext.GetCurrentCustomerAsync()).Id, "Customer")).Where(x => x.Key == "PaymentMethodId").FirstOrDefault();
            var paymentIntentId = (await _genericAttributeService.GetAttributesForEntityAsync((await _workContext.GetCurrentCustomerAsync()).Id, "Customer")).Where(x => x.Key == "PaymentIntentId").FirstOrDefault();


          
            var customer = await _paymentStripeService.GetBuyer(processPaymentRequest.CustomerId);
          
            if (customer == null || customer.Id.IsNullOrEmpty())
                throw new Exception("No Valid Customer Found!");

            var cart = await _shoppingCartService.GetShoppingCartAsync(customer.Customer, ShoppingCartType.ShoppingCart, processPaymentRequest.StoreId);
            if (!cart.Any())
                throw new Exception("No Product Found in Your Cart!");

            StripeConfiguration.ApiKey = stripePaymentSettings.SecretKey;

            var paymentIntentService = new PaymentIntentService();
            PaymentIntentUpdateOptions paymentIntentOptions = new PaymentIntentUpdateOptions();

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


                if (!product.Sku.IsNullOrEmpty())
                    productName = productName + "(" + product.Sku + ")";


                orderItems += Environment.NewLine + productName + " x " + cartItem.Quantity + " (" + productType + ")";
                if (paymentIntentOptions.Metadata==null)
                {
                    paymentIntentOptions.Metadata = new Dictionary<string, string>();
                }
                paymentIntentOptions.Metadata.Add("Item" + (i + 1), "Unit Count:" + cartItem.Quantity + ";" + "Product Name:" + productName + ";" + "Price:" + price + ";" + "Product Type:" + productType);
            }

            paymentIntentOptions.Description = orderItems;
            paymentIntentOptions.PaymentMethod = paymentMethodId.Value;

            var paymentIntentUpdate = await paymentIntentService.UpdateAsync((string)paymentIntentId.Value, paymentIntentOptions, GetStripeApiRequestOptions());
            await _genericAttributeService.DeleteAttributeAsync(paymentMethodId);
            await _genericAttributeService.DeleteAttributeAsync(paymentIntentId);

            var result = new ProcessPaymentResult();
            if (paymentIntentUpdate.Status == "succeeded" || paymentIntentUpdate.Status == "requires_confirmation")
            {

                result.NewPaymentStatus = PaymentStatus.Pending;
                result.AuthorizationTransactionId = paymentIntentUpdate.Id;
                result.AuthorizationTransactionResult = $"Transaction was processed by using {paymentIntentUpdate.LatestCharge?.Source.Object}. Status is {paymentIntentUpdate.Status}";
                return await Task.FromResult(result);
            }
            else
            {
                throw new NopException($"Charge error: {paymentIntentUpdate.StripeResponse}");
            }





        }
    

        public async Task PostProcessPaymentAsync(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            var paymentIntentId = postProcessPaymentRequest.Order.AuthorizationTransactionId;
            var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var stripePaymentSettings = await _settingService.LoadSettingAsync<StripeApplePayPaymentSettings>(storeScope);

            StripeConfiguration.ApiKey = stripePaymentSettings.SecretKey;

            var paymentIntentService = new PaymentIntentService();
            var paymentIntent = await paymentIntentService.ConfirmAsync(paymentIntentId,null, GetStripeApiRequestOptions());

            if (paymentIntent.Status == "succeeded")
            {
                postProcessPaymentRequest.Order.PaymentStatus = PaymentStatus.Paid;
                postProcessPaymentRequest.Order.OrderStatus = OrderStatus.Processing;
               await _orderService.UpdateOrderAsync(postProcessPaymentRequest.Order);
            }
            else
            {
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
            await base.InstallAsync();
        }

        public override async Task UninstallAsync()
        {
            await base.UninstallAsync();
        }

        // IPaymentMethod üyelerinin implementasyonu
        public async Task<bool> CanRePostProcessPaymentAsync(Nop.Core.Domain.Orders.Order order) => false;
        public async Task<CancelRecurringPaymentResult> CancelRecurringPaymentAsync(CancelRecurringPaymentRequest cancelPaymentRequest) => throw new NotImplementedException();
        public async Task<CapturePaymentResult> CaptureAsync(CapturePaymentRequest capturePaymentRequest) => throw new NotImplementedException();
        public bool SupportCapture => false;
        public bool SupportPartiallyRefund => false;
        public bool SupportRefund => false;
        public bool SupportVoid => false;
        public async Task<decimal> GetAdditionalHandlingFeeAsync(IList<ShoppingCartItem> cart) => 0;
        public async Task<IList<string>> ValidatePaymentFormAsync(IFormCollection form) => new List<string>();
        public async Task<ProcessPaymentRequest> GetPaymentInfoAsync(IFormCollection form) => new ProcessPaymentRequest();
        public async Task<bool> HidePaymentMethodAsync(IList<ShoppingCartItem> cart) => false;
        public PaymentMethodType PaymentMethodType => PaymentMethodType.Standard;
        public RecurringPaymentType RecurringPaymentType => RecurringPaymentType.NotSupported;
        public async Task<RefundPaymentResult> RefundAsync(RefundPaymentRequest refundPaymentRequest) => throw new NotImplementedException();
        public async Task<ProcessPaymentResult> ProcessRecurringPaymentAsync(ProcessPaymentRequest processPaymentRequest) => throw new NotImplementedException();
        public async Task<VoidPaymentResult> VoidAsync(VoidPaymentRequest voidPaymentRequest) => throw new NotImplementedException();
        public string GetPublicViewComponentName() => "StripeApplePay";
        public async Task<string> GetPaymentMethodDescriptionAsync() => "Pay with Apple Pay/Google Pay using Stripe.";

      

        public bool SkipPaymentInfo => false;

       // public bool HideInWidgetList =>false;
    }

  
}
