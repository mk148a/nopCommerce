using Moq;
using System.Text.Json;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Discounts;
using Nop.Core.Domain.Logging;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Widgets.GoogleAnalytics;
using Nop.Plugin.Widgets.GoogleAnalytics.Components;
using Nop.Plugin.Widgets.GoogleAnalytics.Services;
using Nop.Services.Catalog;
using Nop.Services.Customers;
using Nop.Services.Discounts;
using Nop.Services.Directory;
using Nop.Services.Events;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Web.Models.Checkout;
using NUnit.Framework;

namespace Nop.Tests.Nop.Plugin.Widgets.GoogleAnalytics.Tests.Components;

[TestFixture]
public class WidgetsGoogleAnalyticsViewComponentTests
{
    private const int CustomerId = 17;
    private const int OrderId = 42;

    [TestCase("Payments.Stripe", "card")]
    [TestCase("Payments.StripeApplePay", "wallet")]
    [TestCase("Payments.StripeKlarna", "klarna")]
    [TestCase("Payments.StripeAffirm", "affirm")]
    [TestCase("Payments.StripeAfterpay", "afterpay_clearpay")]
    [TestCase("Payments.StripeZip", "zip")]
    public async Task PaidStripeOrderEmitsStablePaymentDimensions(string systemName, string expectedPaymentType)
    {
        var component = CreateComponent(CreatePaidOrder(systemName));

        var script = await component.BuildPurchaseScriptAsync(CreateCompletedModel());

        Assert.Multiple(() =>
        {
            Assert.That(script, Does.Contain("\"event\":\"purchase\""));
            Assert.That(script, Does.Contain("\"payment_provider\":\"stripe\""));
            Assert.That(script, Does.Contain($"\"payment_type\":\"{expectedPaymentType}\""));
            Assert.That(script, Does.Contain("\"transaction_id\":\"HOOD-42\""));
            Assert.That(script, Does.Contain("\"ecommerce\":"));
        });
    }

    [TestCase(PaymentStatus.Pending, OrderStatus.Pending)]
    [TestCase(PaymentStatus.Authorized, OrderStatus.Processing)]
    [TestCase(PaymentStatus.Voided, OrderStatus.Cancelled)]
    public async Task NonPaidOrCancelledOrderDoesNotEmitPurchase(PaymentStatus paymentStatus, OrderStatus orderStatus)
    {
        var order = CreatePaidOrder("Payments.StripeKlarna");
        order.PaymentStatus = paymentStatus;
        order.OrderStatus = orderStatus;
        var component = CreateComponent(order);

        var script = await component.BuildPurchaseScriptAsync(CreateCompletedModel());

        Assert.That(script, Is.Empty);
    }

    [Test]
    public async Task EcommerceDisabledDoesNotEmitPurchase()
    {
        var component = CreateComponent(CreatePaidOrder("Payments.Stripe"), settings: new GoogleAnalyticsSettings
        {
            GoogleId = "G-TEST123"
        });

        Assert.That(await component.BuildPurchaseScriptAsync(CreateCompletedModel()), Is.Empty);
    }

    [TestCase("")]
    [TestCase("UA-123456")]
    [TestCase("G-invalid-id!")]
    public async Task EmptyOrInvalidMeasurementIdDoesNotEmitPurchase(string googleId)
    {
        var component = CreateComponent(CreatePaidOrder("Payments.Stripe"), settings: new GoogleAnalyticsSettings
        {
            EnableEcommerce = true,
            GoogleId = googleId
        });

        Assert.That(await component.BuildPurchaseScriptAsync(CreateCompletedModel()), Is.Empty);
    }

    [Test]
    public async Task BlankTrackingScriptDoesNotRenderSecondGtagBootstrap()
    {
        var component = CreateComponent(CreatePaidOrder("Payments.Stripe"), settings: new GoogleAnalyticsSettings
        {
            EnableEcommerce = true,
            GoogleId = "G-TEST123",
            TrackingScript = string.Empty
        });

        Assert.That(await component.BuildTrackingScriptAsync(), Is.Empty);
    }

    [Test]
    public async Task AnotherCustomersPaidOrderDoesNotEmitPurchase()
    {
        var order = CreatePaidOrder("Payments.StripeAffirm");
        order.CustomerId = CustomerId + 1;
        var component = CreateComponent(order);

        var script = await component.BuildPurchaseScriptAsync(CreateCompletedModel());

        Assert.That(script, Is.Empty);
    }

    [TestCase(0)]
    [TestCase(-1)]
    public async Task InvalidCurrencyRateDoesNotEmitPurchase(decimal currencyRate)
    {
        var order = CreatePaidOrder("Payments.StripeKlarna");
        order.CurrencyRate = currencyRate;
        var component = CreateComponent(order);

        var script = await component.BuildPurchaseScriptAsync(CreateCompletedModel());

        Assert.That(script, Is.Empty);
    }

    [TestCase("HOOD-DB-42", "", "HOOD-DB-42")]
    [TestCase("", "HOOD-MODEL-42", "HOOD-MODEL-42")]
    [TestCase("", "", "42")]
    public async Task TransactionIdUsesCanonicalFallbacks(string orderNumber, string completedNumber,
        string expectedTransactionId)
    {
        var order = CreatePaidOrder("Payments.StripeAffirm");
        order.CustomOrderNumber = orderNumber;
        var component = CreateComponent(order);
        var model = CreateCompletedModel();
        model.CustomOrderNumber = completedNumber;

        var script = await component.BuildPurchaseScriptAsync(model);

        Assert.That(script, Does.Contain($"\"transaction_id\":\"{expectedTransactionId}\""));
    }

    [Test]
    public async Task NonStripePaymentIsNotAttributedToStripe()
    {
        var component = CreateComponent(CreatePaidOrder("Payments.CheckMoneyOrder"));

        var script = await component.BuildPurchaseScriptAsync(CreateCompletedModel());

        Assert.Multiple(() =>
        {
            Assert.That(script, Does.Contain("\"payment_provider\":\"other\""));
            Assert.That(script, Does.Contain("\"payment_type\":\"payments.checkmoneyorder\""));
        });
    }

    [Test]
    public async Task PaidOrderIncludesOnlyCustomerCouponCodes()
    {
        var discountService = new Mock<IDiscountService>();
        discountService
            .Setup(service => service.GetAllDiscountUsageHistoryAsync(null, null, OrderId, true, 0, int.MaxValue))
            .ReturnsAsync(new PagedList<DiscountUsageHistory>(new List<DiscountUsageHistory>
            {
                new() { DiscountId = 11, OrderId = OrderId },
                new() { DiscountId = 12, OrderId = OrderId }
            }, 0, int.MaxValue));
        discountService.Setup(service => service.GetDiscountByIdAsync(11)).ReturnsAsync(new Discount
        {
            RequiresCouponCode = true,
            CouponCode = "WELCOME10"
        });
        discountService.Setup(service => service.GetDiscountByIdAsync(12)).ReturnsAsync(new Discount
        {
            RequiresCouponCode = false,
            CouponCode = "AUTOMATIC"
        });
        var component = CreateComponent(CreatePaidOrder("Payments.StripeKlarna"), discountService.Object);

        var script = await component.BuildPurchaseScriptAsync(CreateCompletedModel());

        Assert.Multiple(() =>
        {
            Assert.That(script, Does.Contain("\"coupon\":\"WELCOME10\""));
            Assert.That(script, Does.Not.Contain("AUTOMATIC"));
        });
    }

    [Test]
    public async Task ForeignCurrencyPurchaseConvertsEveryMonetaryFieldAndKeepsTransactionDedupe()
    {
        var order = CreatePaidOrder("Payments.StripeAffirm");
        order.CustomerCurrencyCode = "EUR";
        order.CurrencyRate = 0.8m;
        var item = new OrderItem
        {
            OrderId = order.Id,
            ProductId = 501,
            UnitPriceExclTax = 50m,
            Quantity = 2
        };
        var currencyService = new Mock<ICurrencyService>();
        currencyService
            .Setup(service => service.ConvertCurrency(It.IsAny<decimal>(), 0.8m))
            .Returns((decimal amount, decimal rate) => amount * rate);
        var component = CreateComponent(
            order,
            currencyService: currencyService.Object,
            orderItems: new[] { item },
            products: new Dictionary<int, Product>
            {
                [501] = new() { Id = 501, Name = "Safe historical costume", Sku = "COSTUME-501" }
            });

        var script = await component.BuildPurchaseScriptAsync(CreateCompletedModel());
        using var payload = ParsePayload(script);
        var root = payload.RootElement;
        var ecommerce = root.GetProperty("ecommerce");
        var trackedItem = ecommerce.GetProperty("items")[0];

        Assert.Multiple(() =>
        {
            Assert.That(root.GetProperty("currency").GetString(), Is.EqualTo("EUR"));
            Assert.That(root.GetProperty("value").GetDecimal(), Is.EqualTo(100.4m));
            Assert.That(root.GetProperty("tax").GetDecimal(), Is.EqualTo(6m));
            Assert.That(root.GetProperty("shipping").GetDecimal(), Is.EqualTo(9.6m));
            Assert.That(trackedItem.GetProperty("price").GetDecimal(), Is.EqualTo(40m));
            Assert.That(root.GetProperty("transaction_id").GetString(), Is.EqualTo("HOOD-42"));
            Assert.That(ecommerce.GetProperty("transaction_id").GetString(), Is.EqualTo("HOOD-42"));
            Assert.That(script, Does.Contain("eventCallback"));
            Assert.That(script, Does.Contain("purchase-dispatch/confirm"));
            Assert.That(script, Does.Not.Contain("sessionStorage"));
        });
    }

    [Test]
    public async Task PurchaseReservationFailureDoesNotEmitPurchase()
    {
        var dispatch = new Mock<IGoogleAnalyticsPurchaseDispatchService>();
        dispatch.Setup(service => service.TryReserveAsync(It.IsAny<Order>())).ReturnsAsync((GoogleAnalyticsPurchaseDispatchLease)null);
        var component = CreateComponent(CreatePaidOrder("Payments.Stripe"), dispatchService: dispatch.Object);

        Assert.That(await component.BuildPurchaseScriptAsync(CreateCompletedModel()), Is.Empty);
        dispatch.Verify(service => service.TryReserveAsync(It.Is<Order>(order => order.Id == OrderId)), Times.Once);
    }

    [Test]
    public async Task PaidPurchaseRequiresLeaseBeforeItRendersTheBrowserEvent()
    {
        var dispatch = new Mock<IGoogleAnalyticsPurchaseDispatchService>();
        dispatch.Setup(service => service.TryReserveAsync(It.IsAny<Order>()))
            .ReturnsAsync(new GoogleAnalyticsPurchaseDispatchLease("lease-token", DateTime.UtcNow.AddMinutes(10)));
        var component = CreateComponent(CreatePaidOrder("Payments.Stripe"), dispatchService: dispatch.Object);

        var script = await component.BuildPurchaseScriptAsync(CreateCompletedModel());

        Assert.Multiple(() =>
        {
            Assert.That(script, Does.Contain("\"token\":\"lease-token\""));
            Assert.That(script, Does.Not.Contain("?orderId="));
            Assert.That(script, Does.Contain("URLSearchParams"));
            Assert.That(script, Does.Contain("requestVerificationFieldName"));
            Assert.That(script, Does.Contain("\"requestVerificationFieldName\":\"csrf-field\""));
            Assert.That(script, Does.Contain("b.set(d.requestVerificationFieldName,d.requestVerificationToken)"));
            Assert.That(script, Does.Contain("navigator.sendBeacon"));
            Assert.That(script, Does.Contain("navigator.sendBeacon(u,b))return;if(window.fetch)"));
            Assert.That(script, Does.Contain("eventTimeout=2000"));
        });
    }

    [Test]
    public void BrowserDataLayerIsTheOnlyCanonicalPurchaseTransport()
    {
        var handledEvents = typeof(EventConsumer).GetInterfaces();

        Assert.That(handledEvents, Does.Not.Contain(typeof(IConsumer<OrderPaidEvent>)),
            "OrderPaid Measurement Protocol purchase would duplicate the completed-page dataLayer purchase.");
    }

    [TestCase("G-TEST123", "")]
    [TestCase("", "secret")]
    [TestCase("UA-123456", "secret")]
    [TestCase("G-invalid-id!", "secret")]
    public void MeasurementProtocolRequiresValidIdAndNonEmptySecret(string googleId, string apiSecret)
    {
        Assert.That(EventConsumer.CanSendMeasurementProtocol(new GoogleAnalyticsSettings
        {
            GoogleId = googleId,
            ApiSecret = apiSecret
        }), Is.False);
    }

    [Test]
    public void MeasurementProtocolAllowsValidIdAndNonEmptySecret()
    {
        Assert.That(EventConsumer.CanSendMeasurementProtocol(new GoogleAnalyticsSettings
        {
            GoogleId = "G-TEST123",
            ApiSecret = "secret"
        }), Is.True);
    }

    private static CheckoutCompletedModel CreateCompletedModel()
    {
        return new CheckoutCompletedModel
        {
            OrderId = OrderId,
            CustomOrderNumber = "HOOD-42"
        };
    }

    private static Order CreatePaidOrder(string paymentMethodSystemName)
    {
        return new Order
        {
            Id = OrderId,
            CustomOrderNumber = "HOOD-42",
            CustomerId = CustomerId,
            CustomerCurrencyCode = "usd",
            OrderStatus = OrderStatus.Processing,
            PaymentStatus = PaymentStatus.Paid,
            PaymentMethodSystemName = paymentMethodSystemName,
            CurrencyRate = 1m,
            OrderTotal = 125.50m,
            OrderTax = 7.50m,
            OrderShippingExclTax = 12m
        };
    }

    private static TestWidgetsGoogleAnalyticsViewComponent CreateComponent(
        Order order,
        IDiscountService discountService = null,
        ICurrencyService currencyService = null,
        IList<OrderItem> orderItems = null,
        IReadOnlyDictionary<int, Product> products = null,
        GoogleAnalyticsSettings settings = null,
        IGoogleAnalyticsPurchaseDispatchConfirmationFactory confirmationFactory = null,
        IGoogleAnalyticsPurchaseDispatchService dispatchService = null)
    {
        var orderService = new Mock<IOrderService>();
        orderService.Setup(service => service.GetOrderByIdAsync(OrderId)).ReturnsAsync(order);
        orderService
            .Setup(service => service.GetOrderItemsAsync(OrderId, null, null, 0))
            .ReturnsAsync(orderItems ?? new List<OrderItem>());

        var productService = new Mock<IProductService>();
        productService
            .Setup(service => service.GetProductByIdAsync(It.IsAny<int>()))
            .ReturnsAsync((int productId) => products != null && products.TryGetValue(productId, out var product)
                ? product
                : null);
        productService
            .Setup(service => service.FormatSkuAsync(It.IsAny<Product>(), It.IsAny<string>()))
            .ReturnsAsync((Product product, string _) => product.Sku);

        if (currencyService == null)
        {
            var defaultCurrencyService = new Mock<ICurrencyService>();
            defaultCurrencyService
                .Setup(service => service.ConvertCurrency(It.IsAny<decimal>(), It.IsAny<decimal>()))
                .Returns((decimal amount, decimal _) => amount);
            currencyService = defaultCurrencyService.Object;
        }

        var workContext = new Mock<IWorkContext>();
        workContext.Setup(context => context.GetCurrentCustomerAsync())
            .ReturnsAsync(new Customer { Id = CustomerId });

        dispatchService ??= Mock.Of<IGoogleAnalyticsPurchaseDispatchService>(service =>
            service.TryReserveAsync(It.IsAny<Order>()) == Task.FromResult(
                new GoogleAnalyticsPurchaseDispatchLease("test-lease", DateTime.UtcNow.AddMinutes(10))));
        confirmationFactory ??= Mock.Of<IGoogleAnalyticsPurchaseDispatchConfirmationFactory>(factory =>
            factory.Create() == new GoogleAnalyticsPurchaseDispatchConfirmation(
                "/google-analytics/purchase-dispatch/confirm", "csrf-token", "csrf-field"));

        return new TestWidgetsGoogleAnalyticsViewComponent(
            settings ?? new GoogleAnalyticsSettings
            {
                EnableEcommerce = true,
                GoogleId = "G-TEST123"
            },
            Mock.Of<ICustomerService>(),
            currencyService,
            discountService ?? Mock.Of<IDiscountService>(),
            Mock.Of<ILogger>(),
            orderService.Object,
            productService.Object,
            confirmationFactory,
            dispatchService,
            workContext.Object);
    }

    private static JsonDocument ParsePayload(string script)
    {
        const string marker = "({\"event\"";
        var start = script.IndexOf(marker, StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThanOrEqualTo(0), "The purchase script has no serialized event payload.");
        start++;
        var end = script.IndexOf("},\"/google-analytics/purchase-dispatch/confirm", start, StringComparison.Ordinal);
        Assert.That(end, Is.GreaterThan(start), "The purchase script payload terminator is missing.");
        return JsonDocument.Parse(script[start..(end + 1)]);
    }

    private sealed class TestWidgetsGoogleAnalyticsViewComponent : WidgetsGoogleAnalyticsViewComponent
    {
        public TestWidgetsGoogleAnalyticsViewComponent(
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
            : base(googleAnalyticsSettings, customerService, currencyService, discountService, logger, orderService,
                productService, purchaseDispatchConfirmationFactory, purchaseDispatchService, workContext)
        {
        }

        public Task<string> BuildPurchaseScriptAsync(CheckoutCompletedModel model)
        {
            return GetPurchaseScriptAsync(model);
        }

        public Task<string> BuildTrackingScriptAsync()
        {
            return GetScriptAsync();
        }
    }
}
