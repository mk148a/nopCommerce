using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using Nop.Core;
using Nop.Core.Domain.Logging;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Widgets.GoogleAnalytics.Models;
using Nop.Plugin.Widgets.GoogleAnalytics.Services;
using Nop.Services.Catalog;
using Nop.Services.Customers;
using Nop.Services.Discounts;
using Nop.Services.Directory;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Web.Framework.Components;
using Nop.Web.Framework.Infrastructure;
using Nop.Web.Models.Checkout;
using Nop.Web.Models.Order;

namespace Nop.Plugin.Widgets.GoogleAnalytics.Components;

public class WidgetsGoogleAnalyticsViewComponent : NopViewComponent
{
    #region Fields

    protected readonly GoogleAnalyticsSettings _googleAnalyticsSettings;
    protected readonly ICustomerService _customerService;
    protected readonly ICurrencyService _currencyService;
    protected readonly IDiscountService _discountService;
    protected readonly ILogger _logger;
    protected readonly IOrderService _orderService;
    protected readonly IProductService _productService;
    protected readonly IGoogleAnalyticsPurchaseDispatchConfirmationFactory _purchaseDispatchConfirmationFactory;
    protected readonly IGoogleAnalyticsPurchaseDispatchService _purchaseDispatchService;
    protected readonly IWorkContext _workContext;

    #endregion

    #region Ctor

    public WidgetsGoogleAnalyticsViewComponent(
        GoogleAnalyticsSettings googleAnalyticsSettings,
        ICustomerService customerService,
        ICurrencyService currencyService,
        IDiscountService discountService,
        ILogger logger,
        IOrderService orderService,
        IProductService productService,
        IGoogleAnalyticsPurchaseDispatchConfirmationFactory purchaseDispatchConfirmationFactory,
        IGoogleAnalyticsPurchaseDispatchService purchaseDispatchService,
        IWorkContext workContext)
    {
        _googleAnalyticsSettings = googleAnalyticsSettings;
        _customerService = customerService;
        _currencyService = currencyService;
        _discountService = discountService;
        _logger = logger;
        _orderService = orderService;
        _productService = productService;
        _purchaseDispatchConfirmationFactory = purchaseDispatchConfirmationFactory;
        _purchaseDispatchService = purchaseDispatchService;
        _workContext = workContext;
    }

    #endregion

    #region Utilities

    /// <returns>A task that represents the asynchronous operation</returns>
    protected async Task<string> GetScriptAsync()
    {
        try
        {
            // This plugin can be configured as a paid-order dataLayer producer
            // behind an existing GTM container. Never render a second gtag
            // bootstrap/page-view path for an empty or malformed ID/script.
            if (!HasValidMeasurementId() || string.IsNullOrWhiteSpace(_googleAnalyticsSettings.TrackingScript))
                return string.Empty;

            var analyticsTrackingScript = _googleAnalyticsSettings.TrackingScript + "\n";
            analyticsTrackingScript = analyticsTrackingScript.Replace("{GOOGLEID}", _googleAnalyticsSettings.GoogleId);
            //remove {ECOMMERCE} (used in previous versions of the plugin)
            analyticsTrackingScript = analyticsTrackingScript.Replace("{ECOMMERCE}", "");
            //remove {CustomerID} (used in previous versions of the plugin)
            analyticsTrackingScript = analyticsTrackingScript.Replace("{CustomerID}", "");

            //whether to include customer identifier
            var customerIdCode = string.Empty;
            var customer = await _workContext.GetCurrentCustomerAsync();
            if (_googleAnalyticsSettings.IncludeCustomerId && !await _customerService.IsGuestAsync(customer))
                customerIdCode = $"gtag('set', {{'user_id': '{customer.Id}'}});{Environment.NewLine}";
            analyticsTrackingScript = analyticsTrackingScript.Replace("{CUSTOMER_TRACKING}", customerIdCode);
            analyticsTrackingScript = analyticsTrackingScript.Replace("{ECOMMERCE_TRACKING}", "");

            return analyticsTrackingScript;
        }
        catch (Exception ex)
        {
            await _logger.InsertLogAsync(LogLevel.Error, "Error creating scripts for Google eCommerce tracking", ex.ToString());
        }

        return "";
    }

    /// <summary>
    /// Creates the standard GA4 purchase data-layer event for a successfully
    /// paid order. The completed page is customer-scoped, so this never exposes
    /// another customer's order details.
    /// </summary>
    protected async Task<string> GetPurchaseScriptAsync(int orderId, string customOrderNumber)
    {
        // The checkout dataLayer event is intentionally configuration-gated.
        // Keeping this false leaves GTM as the lifecycle-only integration; a
        // valid GA4 ID makes this the single paid-purchase owner without any
        // Measurement Protocol/API-secret dependency.
        if (!_googleAnalyticsSettings.EnableEcommerce || !HasValidMeasurementId())
            return string.Empty;

        var order = await _orderService.GetOrderByIdAsync(orderId);
        var customer = await _workContext.GetCurrentCustomerAsync();

        // A conversion represents a successfully captured/paid transaction.
        // Authorized orders may still be cancelled or fail capture, so they
        // must not emit the purchase data layer event.
        if (order == null || order.CustomerId != customer.Id ||
            !GoogleAnalyticsPurchaseEligibility.CanEmit(order))
            return string.Empty;

        // Order monetary values are stored in the primary store currency.  A
        // non-positive historical rate cannot be converted reliably, so fail
        // closed instead of labelling an unconverted amount as customer
        // currency in GA4 or Google Ads.
        if (order.CurrencyRate <= decimal.Zero)
            return string.Empty;

        // Generate this before claiming the database lease. A routing or CSRF
        // failure must not consume an order whose completed page cannot confirm.
        GoogleAnalyticsPurchaseDispatchConfirmation confirmation;
        try
        {
            confirmation = _purchaseDispatchConfirmationFactory.Create();
        }
        catch (Exception ex)
        {
            await _logger.InsertLogAsync(LogLevel.Error, "Unable to create Google Analytics purchase dispatch confirmation", ex.ToString());
            return string.Empty;
        }

        // The database lease is acquired only after all ownership and paid-state
        // checks pass. Its unique OrderId contract suppresses concurrent tabs,
        // sessions and devices. This is at-least-once recovery until browser
        // confirmation; canonical transaction_id is the downstream idempotency
        // boundary if a response is rendered but its confirmation is lost.
        var lease = await _purchaseDispatchService.TryReserveAsync(order);
        if (lease == null)
            return string.Empty;

        var currency = string.IsNullOrWhiteSpace(order.CustomerCurrencyCode)
            ? "USD"
            : order.CustomerCurrencyCode.Trim().ToUpperInvariant();
        var currencyRate = order.CurrencyRate;
        var transactionId = !string.IsNullOrWhiteSpace(order.CustomOrderNumber)
            ? order.CustomOrderNumber.Trim()
            : !string.IsNullOrWhiteSpace(customOrderNumber)
                ? customOrderNumber.Trim()
                : order.Id.ToString(CultureInfo.InvariantCulture);
        var paymentTracking = ResolvePaymentTracking(order.PaymentMethodSystemName);
        var coupon = await GetCouponCodesAsync(order.Id);

        var items = new List<object>();
        foreach (var orderItem in await _orderService.GetOrderItemsAsync(order.Id))
        {
            var product = await _productService.GetProductByIdAsync(orderItem.ProductId);
            if (product == null)
                continue;

            var sku = await _productService.FormatSkuAsync(product, orderItem.AttributesXml);
            if (string.IsNullOrWhiteSpace(sku))
                sku = product.Id.ToString(CultureInfo.InvariantCulture);

            items.Add(new
            {
                item_id = sku,
                item_name = product.Name,
                price = _currencyService.ConvertCurrency(orderItem.UnitPriceExclTax, currencyRate),
                quantity = orderItem.Quantity
            });
        }

        var purchase = new
        {
            transaction_id = transactionId,
            value = _currencyService.ConvertCurrency(order.OrderTotal, currencyRate),
            currency,
            tax = _currencyService.ConvertCurrency(order.OrderTax, currencyRate),
            shipping = _currencyService.ConvertCurrency(order.OrderShippingExclTax, currencyRate),
            coupon,
            payment_provider = paymentTracking.PaymentProvider,
            payment_type = paymentTracking.PaymentType,
            items
        };

        // The top-level fields support the existing GTM container; the nested
        // ecommerce object supports standard GA4 ecommerce tags.
        var payload = new
        {
            @event = "purchase",
            transaction_id = purchase.transaction_id,
            value = purchase.value,
            currency = purchase.currency,
            tax = purchase.tax,
            shipping = purchase.shipping,
            coupon = purchase.coupon,
            payment_provider = purchase.payment_provider,
            payment_type = purchase.payment_type,
            items = purchase.items,
            ecommerce = purchase
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.Default,
            PropertyNamingPolicy = null
        });

        var confirmationJson = JsonSerializer.Serialize(new
        {
            orderId = order.Id,
            token = lease.Token,
            requestVerificationToken = confirmation.RequestVerificationToken,
            requestVerificationFieldName = confirmation.RequestVerificationFieldName
        }, new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.Default
        });
        var confirmUrlJson = JsonSerializer.Serialize(confirmation.Url, new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.Default
        });

        // The acknowledgement follows dataLayer delivery (or its bounded GTM
        // timeout). The POST body carries both lease capability and antiforgery
        // token; neither appears in URLs, logs, referrers, or browser history.
        return $"<script>(function(p,u,d){{var done=false;function confirm(){{if(done)return;done=true;try{{var b=new URLSearchParams();b.set('orderId',String(d.orderId));b.set('token',d.token);b.set(d.requestVerificationFieldName,d.requestVerificationToken);if(navigator.sendBeacon&&navigator.sendBeacon(u,b))return;if(window.fetch)window.fetch(u,{{method:'POST',credentials:'same-origin',keepalive:true,headers:{{'Content-Type':'application/x-www-form-urlencoded;charset=UTF-8'}},body:b.toString()}});}}catch(e){{}}}}p.eventCallback=confirm;p.eventTimeout=2000;window.dataLayer=window.dataLayer||[];window.dataLayer.push({{ecommerce:null}});window.dataLayer.push(p);window.setTimeout(confirm,2500);}})({json},{confirmUrlJson},{confirmationJson});</script>";
    }

    // Kept as a compatibility overload for existing component tests and
    // callers while the order-details fallback shares the same implementation.
    protected Task<string> GetPurchaseScriptAsync(CheckoutCompletedModel completedModel)
        => GetPurchaseScriptAsync(completedModel.OrderId, completedModel.CustomOrderNumber);

    /// <summary>
    /// Determines whether a configured Google Analytics 4 Measurement ID is
    /// safe to use. The dataLayer purchase path does not load gtag itself,
    /// but it must not activate from a blank placeholder or a legacy ID.
    /// </summary>
    protected virtual bool HasValidMeasurementId()
    {
        var measurementId = _googleAnalyticsSettings.GoogleId?.Trim();
        return !string.IsNullOrWhiteSpace(measurementId) &&
               measurementId.StartsWith("G-", StringComparison.OrdinalIgnoreCase) &&
               measurementId.Length > 2 &&
               measurementId[2..].All(char.IsLetterOrDigit);
    }

    /// <summary>
    /// Maps nopCommerce payment system names to stable analytics dimensions.
    /// Unknown payment methods intentionally remain identifiable without being
    /// attributed to Stripe.
    /// </summary>
    protected virtual (string PaymentProvider, string PaymentType) ResolvePaymentTracking(string paymentMethodSystemName)
    {
        return paymentMethodSystemName switch
        {
            "Payments.Stripe" => ("stripe", "card"),
            "Payments.StripeApplePay" => ("stripe", "wallet"),
            "Payments.StripeKlarna" => ("stripe", "klarna"),
            "Payments.StripeAffirm" => ("stripe", "affirm"),
            "Payments.StripeAfterpay" => ("stripe", "afterpay_clearpay"),
            "Payments.StripeZip" => ("stripe", "zip"),
            _ => ("other", string.IsNullOrWhiteSpace(paymentMethodSystemName)
                ? "unknown"
                : paymentMethodSystemName.Trim().ToLowerInvariant())
        };
    }

    /// <summary>
    /// Gets customer-entered coupon codes associated with the completed order.
    /// Automatic discounts intentionally don't masquerade as coupon codes.
    /// </summary>
    protected virtual async Task<string> GetCouponCodesAsync(int orderId)
    {
        var usageHistory = await _discountService.GetAllDiscountUsageHistoryAsync(orderId: orderId);
        if (usageHistory == null || usageHistory.Count == 0)
            return string.Empty;

        var couponCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var usage in usageHistory)
        {
            var discount = await _discountService.GetDiscountByIdAsync(usage.DiscountId);
            if (discount?.RequiresCouponCode == true && !string.IsNullOrWhiteSpace(discount.CouponCode))
                couponCodes.Add(discount.CouponCode.Trim());
        }

        return string.Join(",", couponCodes.OrderBy(code => code, StringComparer.OrdinalIgnoreCase));
    }

    #endregion

    #region Methods

    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task<IViewComponentResult> InvokeAsync(string widgetZone, object additionalData)
    {
        if (widgetZone.Equals(PublicWidgetZones.CheckoutCompletedBottom, StringComparison.OrdinalIgnoreCase) &&
            additionalData is CheckoutCompletedModel completedModel)
            // PublicInfo.cshtml renders the supplied script with Html.Raw.  A
            // ContentViewComponentResult is encoded by the WidgetViewComponent
            // aggregator and would leave the script visible as text instead of
            // executing it in the completed-page DOM.
            return View("~/Plugins/Widgets.GoogleAnalytics/Views/PublicInfo.cshtml",
                await GetPurchaseScriptAsync(completedModel.OrderId, completedModel.CustomOrderNumber));

        // Some payment methods redirect directly to the authenticated order
        // details page instead of rendering CheckoutCompleted. Keep the same
        // paid-state, ownership and durable lease checks there so the purchase
        // event is not lost, while the OrderId-unique dispatch row still
        // prevents duplicate GA4/Ads purchases on refresh.
        if (widgetZone.Equals(PublicWidgetZones.OrderDetailsPageBottom, StringComparison.OrdinalIgnoreCase) &&
            additionalData is OrderDetailsModel orderDetailsModel)
            return View("~/Plugins/Widgets.GoogleAnalytics/Views/PublicInfo.cshtml",
                await GetPurchaseScriptAsync(orderDetailsModel.Id, orderDetailsModel.CustomOrderNumber));

        var script = await GetScriptAsync();
        return View("~/Plugins/Widgets.GoogleAnalytics/Views/PublicInfo.cshtml", script);
    }

    #endregion
}
