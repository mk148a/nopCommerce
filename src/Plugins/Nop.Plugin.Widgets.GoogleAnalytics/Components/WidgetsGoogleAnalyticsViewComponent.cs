using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using Nop.Core;
using Nop.Core.Domain.Logging;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Widgets.GoogleAnalytics.Models;
using Nop.Services.Catalog;
using Nop.Services.Customers;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Web.Framework.Components;
using Nop.Web.Framework.Infrastructure;
using Nop.Web.Models.Checkout;

namespace Nop.Plugin.Widgets.GoogleAnalytics.Components;

public class WidgetsGoogleAnalyticsViewComponent : NopViewComponent
{
    #region Fields

    protected readonly GoogleAnalyticsSettings _googleAnalyticsSettings;
    protected readonly ICustomerService _customerService;
    protected readonly ILogger _logger;
    protected readonly IOrderService _orderService;
    protected readonly IProductService _productService;
    protected readonly IWorkContext _workContext;

    #endregion

    #region Ctor

    public WidgetsGoogleAnalyticsViewComponent(
        GoogleAnalyticsSettings googleAnalyticsSettings,
        ICustomerService customerService,
        ILogger logger,
        IOrderService orderService,
        IProductService productService,
        IWorkContext workContext)
    {
        _googleAnalyticsSettings = googleAnalyticsSettings;
        _customerService = customerService;
        _logger = logger;
        _orderService = orderService;
        _productService = productService;
        _workContext = workContext;
    }

    #endregion

    #region Utilities

    /// <returns>A task that represents the asynchronous operation</returns>
    protected async Task<string> GetScriptAsync()
    {
        try
        {
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
    protected async Task<string> GetPurchaseScriptAsync(CheckoutCompletedModel completedModel)
    {
        var order = await _orderService.GetOrderByIdAsync(completedModel.OrderId);
        var customer = await _workContext.GetCurrentCustomerAsync();

        // A conversion represents a successfully captured/paid transaction.
        // Authorized orders may still be cancelled or fail capture, so they
        // must not emit the purchase data layer event.
        if (order == null || order.Deleted || order.CustomerId != customer.Id ||
            order.PaymentStatus != PaymentStatus.Paid)
            return string.Empty;

        var currency = string.IsNullOrWhiteSpace(order.CustomerCurrencyCode)
            ? "USD"
            : order.CustomerCurrencyCode.Trim().ToUpperInvariant();

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
                price = orderItem.UnitPriceExclTax,
                quantity = orderItem.Quantity
            });
        }

        var purchase = new
        {
            transaction_id = completedModel.CustomOrderNumber,
            value = order.OrderTotal,
            currency,
            tax = order.OrderTax,
            shipping = order.OrderShippingExclTax,
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
            items = purchase.items,
            ecommerce = purchase
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.Default,
            PropertyNamingPolicy = null
        });

        return $"<script>(function(p){{try{{var k='hood_ga4_purchase_'+p.transaction_id;if(sessionStorage.getItem(k))return;sessionStorage.setItem(k,'1');window.dataLayer=window.dataLayer||[];window.dataLayer.push(p)}}catch(e){{window.dataLayer=window.dataLayer||[];window.dataLayer.push(p)}}}})({json});</script>";
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
                await GetPurchaseScriptAsync(completedModel));

        var script = await GetScriptAsync();
        return View("~/Plugins/Widgets.GoogleAnalytics/Views/PublicInfo.cshtml", script);
    }

    #endregion
}
