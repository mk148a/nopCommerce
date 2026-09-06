using FluentAssertions;
using Moq;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Data;
using Nop.Plugin.Widgets.GoogleAnalytics;
using Nop.Plugin.Widgets.GoogleAnalytics.Components;
using Nop.Plugin.Widgets.GoogleAnalytics.Domains;
using Nop.Plugin.Widgets.GoogleAnalytics.Services;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Web.Models.Checkout;
using NUnit.Framework;

namespace Nop.Tests.Nop.Services.Tests.GoogleAnalytics;

[TestFixture]
public class GoogleAnalyticsPurchaseDispatchTests
{
    [TestCase(PaymentStatus.Pending)]
    [TestCase(PaymentStatus.Authorized)]
    [TestCase(PaymentStatus.Voided)]
    public void CanEmit_returns_false_until_payment_is_paid(PaymentStatus paymentStatus)
    {
        var order = CreateOrder(paymentStatus);

        GoogleAnalyticsPurchaseEligibility.CanEmit(order).Should().BeFalse();
    }

    [Test]
    public void CanEmit_returns_false_for_cancelled_paid_order()
    {
        var order = CreateOrder(PaymentStatus.Paid);
        order.OrderStatus = OrderStatus.Cancelled;

        GoogleAnalyticsPurchaseEligibility.CanEmit(order).Should().BeFalse();
    }

    [Test]
    public async Task TryReserve_paid_order_returns_true_once_and_false_on_refresh()
    {
        var records = new List<GoogleAnalyticsPurchaseDispatch>();
        var repository = new Mock<IRepository<GoogleAnalyticsPurchaseDispatch>>();
        repository.SetupGet(item => item.Table).Returns(() => records.AsQueryable());
        repository.Setup(item => item.InsertAsync(It.IsAny<GoogleAnalyticsPurchaseDispatch>(), false))
            .Callback<GoogleAnalyticsPurchaseDispatch, bool>((record, _) => records.Add(record))
            .Returns(Task.CompletedTask);

        var attributes = new Mock<IGenericAttributeService>();
        var service = new GoogleAnalyticsPurchaseDispatchService(repository.Object, attributes.Object);
        var order = CreateOrder(PaymentStatus.Paid);

        (await service.TryReserveAsync(order)).Should().BeTrue();
        (await service.TryReserveAsync(order)).Should().BeFalse();
        records.Should().ContainSingle(record => record.OrderId == order.Id);
    }

    [Test]
    public async Task TryReserve_stripe_order_without_signed_webhook_marker_returns_false()
    {
        var repository = new Mock<IRepository<GoogleAnalyticsPurchaseDispatch>>();
        var attributes = new Mock<IGenericAttributeService>();
        attributes.Setup(service => service.GetAttributeAsync<string>(
                It.IsAny<BaseEntity>(),
                "GoogleAnalytics.StripeWebhookVerifiedPaymentIntent",
                It.IsAny<int>(),
                It.IsAny<string>()))
            .ReturnsAsync((string)null);
        var service = new GoogleAnalyticsPurchaseDispatchService(repository.Object, attributes.Object);
        var order = CreateOrder(PaymentStatus.Paid);
        order.PaymentMethodSystemName = "Payments.Stripe";

        (await service.TryReserveAsync(order)).Should().BeFalse();
        repository.Verify(item => item.InsertAsync(It.IsAny<GoogleAnalyticsPurchaseDispatch>(), false), Times.Never);
    }

    [Test]
    public async Task TryReserve_stripe_order_with_signed_webhook_marker_returns_true_once()
    {
        var records = new List<GoogleAnalyticsPurchaseDispatch>();
        var repository = new Mock<IRepository<GoogleAnalyticsPurchaseDispatch>>();
        repository.SetupGet(item => item.Table).Returns(() => records.AsQueryable());
        repository.Setup(item => item.InsertAsync(It.IsAny<GoogleAnalyticsPurchaseDispatch>(), false))
            .Callback<GoogleAnalyticsPurchaseDispatch, bool>((record, _) => records.Add(record))
            .Returns(Task.CompletedTask);

        var attributes = new Mock<IGenericAttributeService>();
        attributes.Setup(service => service.GetAttributeAsync<string>(
                It.IsAny<BaseEntity>(),
                "GoogleAnalytics.StripeWebhookVerifiedPaymentIntent",
                It.IsAny<int>(),
                It.IsAny<string>()))
            .ReturnsAsync("pi_webhook_verified");
        var service = new GoogleAnalyticsPurchaseDispatchService(repository.Object, attributes.Object);
        var order = CreateOrder(PaymentStatus.Paid);
        order.PaymentMethodSystemName = "Payments.Stripe";

        (await service.TryReserveAsync(order)).Should().BeTrue();
        (await service.TryReserveAsync(order)).Should().BeFalse();
        records.Should().ContainSingle(record => record.OrderId == order.Id);
    }

    [Test]
    public async Task Completed_page_for_expired_payment_renders_no_purchase_event()
    {
        var order = CreateOrder(PaymentStatus.Voided);
        var dispatch = new Mock<IGoogleAnalyticsPurchaseDispatchService>();
        var component = CreateComponent(order, dispatch.Object);

        var markup = await component.RenderPurchaseAsync(new CheckoutCompletedModel { OrderId = order.Id });

        markup.Should().BeEmpty();
        dispatch.Verify(service => service.TryReserveAsync(It.IsAny<Order>()), Times.Never);
    }

    [Test]
    public async Task Completed_page_for_paid_order_renders_one_dataLayer_purchase_with_real_order_id()
    {
        var order = CreateOrder(PaymentStatus.Paid);
        var dispatch = new Mock<IGoogleAnalyticsPurchaseDispatchService>();
        dispatch.Setup(service => service.TryReserveAsync(order)).ReturnsAsync(true);
        var component = CreateComponent(order, dispatch.Object);

        var markup = await component.RenderPurchaseAsync(new CheckoutCompletedModel { OrderId = order.Id });

        markup.Should().Contain("dataLayer.push(p)");
        markup.Should().Contain("\"event\":\"purchase\"");
        markup.Should().Contain("\"transaction_id\":\"863\"");
        markup.Should().NotContain("gtag('event'");
    }

    [Test]
    public async Task Refresh_after_paid_purchase_renders_no_second_purchase_event()
    {
        var order = CreateOrder(PaymentStatus.Paid);
        var dispatch = new Mock<IGoogleAnalyticsPurchaseDispatchService>();
        dispatch.SetupSequence(service => service.TryReserveAsync(order))
            .ReturnsAsync(true)
            .ReturnsAsync(false);
        var component = CreateComponent(order, dispatch.Object);
        var completed = new CheckoutCompletedModel { OrderId = order.Id };

        var initialMarkup = await component.RenderPurchaseAsync(completed);
        var refreshMarkup = await component.RenderPurchaseAsync(completed);

        initialMarkup.Should().Contain("\"event\":\"purchase\"");
        refreshMarkup.Should().BeEmpty();
    }

    private static TestableGoogleAnalyticsComponent CreateComponent(Order order,
        IGoogleAnalyticsPurchaseDispatchService dispatchService)
    {
        var orderService = new Mock<IOrderService>();
        orderService.Setup(service => service.GetOrderByIdAsync(order.Id)).ReturnsAsync(order);
        orderService.Setup(service => service.GetOrderItemsAsync(order.Id, null, null, 0))
            .ReturnsAsync(new List<OrderItem>());

        var workContext = new Mock<IWorkContext>();
        workContext.Setup(context => context.GetCurrentCustomerAsync())
            .ReturnsAsync(new global::Nop.Core.Domain.Customers.Customer { Id = order.CustomerId });

        return new TestableGoogleAnalyticsComponent(
            new GoogleAnalyticsSettings(),
            Mock.Of<ICustomerService>(),
            Mock.Of<ILogger>(),
            orderService.Object,
            Mock.Of<IProductService>(),
            dispatchService,
            workContext.Object);
    }

    private static Order CreateOrder(PaymentStatus paymentStatus)
    {
        return new Order
        {
            Id = 863,
            CustomerId = 42,
            StoreId = 1,
            PaymentMethodSystemName = "Payments.CheckMoneyOrder",
            OrderStatus = OrderStatus.Pending,
            PaymentStatus = paymentStatus
        };
    }

    private sealed class TestableGoogleAnalyticsComponent : WidgetsGoogleAnalyticsViewComponent
    {
        public TestableGoogleAnalyticsComponent(GoogleAnalyticsSettings googleAnalyticsSettings,
            ICustomerService customerService,
            ILogger logger,
            IOrderService orderService,
            IProductService productService,
            IGoogleAnalyticsPurchaseDispatchService purchaseDispatchService,
            IWorkContext workContext)
            : base(googleAnalyticsSettings, customerService, logger, orderService, productService,
                purchaseDispatchService, workContext)
        {
        }

        public Task<string> RenderPurchaseAsync(CheckoutCompletedModel completedModel)
        {
            return GetPurchaseScriptAsync(completedModel);
        }
    }
}
