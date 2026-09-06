using Moq;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Misc.StripeBnplCore;
using Nop.Plugin.Misc.StripeBnplCore.Domain;
using Nop.Plugin.Misc.StripeBnplCore.Services;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Directory;
using Nop.Services.Logging;
using Nop.Services.Orders;
using NUnit.Framework;
using Stripe;
using Stripe.Checkout;

namespace Nop.Tests.Nop.Plugin.Misc.StripeBnplCore.Tests;

[TestFixture]
public class StripeBnplCheckoutServiceTests
{
    [Test]
    public async Task CheckoutUsesProviderConfigurationWithoutPaymentMethodTypesAndReconcilesAmount()
    {
        var fixture = CreateFixture(returnedAmountMinor: 12_550);

        var result = await fixture.Service.CreateAsync(BnplProvider.Klarna, fixture.Order);

        var options = fixture.CapturedOptions;
        var requestOptions = fixture.CapturedRequestOptions;
        var allocatedAmount = options.LineItems.Sum(item => item.PriceData.UnitAmount.GetValueOrDefault() * item.Quantity);

        Assert.Multiple(() =>
        {
            Assert.That(options.Mode, Is.EqualTo("payment"));
            Assert.That(options.PaymentMethodConfiguration, Is.EqualTo("pmc_test_klarna"));
            Assert.That(options.PaymentMethodTypes, Is.Null,
                "Dynamic payment methods must be controlled by Payment Method Configuration, not payment_method_types.");
            Assert.That(options.IntegrationIdentifier, Does.StartWith("nop_bnpl_klarna_"));
            Assert.That(options.IntegrationIdentifier.Length, Is.EqualTo("nop_bnpl_klarna_".Length + 8));
            Assert.That(options.IntegrationIdentifier[^8..], Does.Match("^[a-z]{8}$"));
            Assert.That(options.Metadata["provider"], Is.EqualTo("klarna"));
            Assert.That(options.Metadata["payment_system_name"], Is.EqualTo("Payments.StripeKlarna"));
            Assert.That(options.Metadata["eligibility_snapshot_hash"], Has.Length.EqualTo(64));
            Assert.That(options.PaymentIntentData.Metadata["eligibility_snapshot_hash"],
                Is.EqualTo(options.Metadata["eligibility_snapshot_hash"]));
            Assert.That(allocatedAmount, Is.EqualTo(12_550));
            Assert.That(requestOptions.IdempotencyKey,
                Is.EqualTo($"nop-bnpl-session-{fixture.Order.OrderGuid:N}-{(int)BnplProvider.Klarna}-a1"));
            Assert.That(result.AmountMinor, Is.EqualTo(12_550));
            Assert.That(result.Currency, Is.EqualTo("usd"));
            Assert.That(fixture.Order.AuthorizationTransactionId, Is.EqualTo("pi_test_klarna"));
        });

        fixture.SessionStore.Verify(store => store.ReserveAttemptAsync(
            fixture.Order.Id,
            fixture.Order.OrderGuid,
            BnplProvider.Klarna,
            12_550,
            "usd",
            true,
            It.Is<StripeBnplEligibilitySnapshotEnvelope>(snapshot => !string.IsNullOrWhiteSpace(snapshot.Sha256))),
            Times.Once);
        fixture.SessionStore.Verify(store => store.CompleteAttemptAsync(
            It.Is<StripeBnplCheckoutSession>(attempt => attempt.OrderId == fixture.Order.Id &&
                attempt.AttemptNumber == 1),
            It.Is<Session>(session => session.Id == "cs_test_klarna")), Times.Once);
        fixture.ConfigurationClient.Verify(client => client.EnsureProviderOnlyAsync(
            BnplProvider.Klarna, "pmc_test_klarna", false), Times.Once);
        fixture.OrderService.Verify(service => service.UpdateOrderAsync(fixture.Order), Times.Once);
    }

    [Test]
    public void StripeAmountMismatchIsRecordedButNeverMarksOrReturnsTheOrder()
    {
        var fixture = CreateFixture(returnedAmountMinor: 12_549);

        var exception = Assert.ThrowsAsync<NopException>(async () =>
            await fixture.Service.CreateAsync(BnplProvider.Klarna, fixture.Order));

        Assert.That(exception.Message, Does.Contain("does not match"));
        fixture.SessionStore.Verify(store => store.CompleteAttemptAsync(
            It.IsAny<StripeBnplCheckoutSession>(),
            It.Is<Session>(session => session.AmountTotal == 12_549)), Times.Once);
        fixture.OrderService.Verify(service => service.UpdateOrderAsync(It.IsAny<Order>()), Times.Never);
    }

    [Test]
    public void ProviderMustMatchThePendingOrdersSelectedPaymentMethod()
    {
        var fixture = CreateFixture(returnedAmountMinor: 12_550);

        var exception = Assert.ThrowsAsync<NopException>(async () =>
            await fixture.Service.CreateAsync(BnplProvider.Affirm, fixture.Order));

        Assert.That(exception.Message, Does.Contain("does not match"));
        fixture.SessionClient.Verify(client => client.CreateAsync(
            It.IsAny<SessionCreateOptions>(), It.IsAny<RequestOptions>()), Times.Never);
    }

    private static CheckoutFixture CreateFixture(long returnedAmountMinor)
    {
        var order = new Order
        {
            Id = 42,
            OrderGuid = Guid.Parse("11111111-2222-3333-4444-555555555555"),
            CustomOrderNumber = "HOOD-42",
            CustomerId = 17,
            StoreId = 1,
            BillingAddressId = 5,
            CustomerCurrencyCode = "USD",
            CurrencyRate = 1m,
            PaymentMethodSystemName = "Payments.StripeKlarna",
            PaymentStatus = PaymentStatus.Pending,
            OrderStatus = OrderStatus.Pending,
            OrderTotal = 125.50m,
            OrderTax = 7.50m,
            OrderShippingInclTax = 12m,
            OrderDiscount = 5m,
            OrderSubTotalDiscountInclTax = 0m
        };
        var orderItems = new List<OrderItem>
        {
            new()
            {
                Id = 101,
                OrderId = order.Id,
                ProductId = 501,
                Quantity = 2,
                PriceInclTax = 100m
            }
        };

        var eligibilityService = new Mock<IBnplEligibilityService>();
        eligibilityService
            .Setup(service => service.EvaluateOrderAsync(BnplProvider.Klarna, order, orderItems))
            .ReturnsAsync(BnplCartEligibilityResult.Eligible());
        var eligibilitySnapshot = CreateEligibilitySnapshot(order, orderItems);
        eligibilityService.Setup(service => service.CaptureOrderSnapshotAsync(
                BnplProvider.Klarna, order, orderItems, 12_550, "usd"))
            .ReturnsAsync(eligibilitySnapshot);

        var orderService = new Mock<IOrderService>();
        orderService
            .Setup(service => service.GetOrderItemsAsync(order.Id, null, null, 0))
            .ReturnsAsync(orderItems);
        orderService.Setup(service => service.UpdateOrderAsync(order)).Returns(Task.CompletedTask);

        var currencyService = new Mock<ICurrencyService>();
        currencyService
            .Setup(service => service.ConvertCurrency(It.IsAny<decimal>(), 1m))
            .Returns((decimal amount, decimal _) => amount);

        var productService = new Mock<IProductService>();
        productService.Setup(service => service.GetProductByIdAsync(501)).ReturnsAsync(new global::Nop.Core.Domain.Catalog.Product
        {
            Id = 501,
            Name = "Safe historical costume",
            Published = true
        });

        var addressService = new Mock<IAddressService>();
        addressService.Setup(service => service.GetAddressByIdAsync(5))
            .ReturnsAsync(new global::Nop.Core.Domain.Common.Address { Id = 5, Email = "buyer@example.test" });

        var environmentGuard = new Mock<IStripeBnplEnvironmentGuard>();
        environmentGuard.Setup(guard => guard.EnsureSafe());

        var webHelper = new Mock<IWebHelper>();
        webHelper.Setup(helper => helper.GetStoreLocation(null)).Returns("https://test.hoodarcheryshop.invalid/");

        var sessionStore = new Mock<IStripeBnplCheckoutSessionStore>();
        var reservedAttempt = new StripeBnplCheckoutSession
        {
            Id = 77,
            OrderId = order.Id,
            Provider = BnplProvider.Klarna,
            SessionId = "pending:nop-bnpl-session",
            AmountMinor = 12_550,
            Currency = "usd",
            Status = "creating",
            AttemptNumber = 1,
            IdempotencyKey = $"nop-bnpl-session-{order.OrderGuid:N}-{(int)BnplProvider.Klarna}-a1",
            ExpiresOnUtc = DateTime.UtcNow.AddMinutes(35),
            IsSandbox = true,
            EligibilitySnapshotJson = eligibilitySnapshot.Json,
            EligibilitySnapshotHash = eligibilitySnapshot.Sha256
        };
        sessionStore.Setup(store => store.ReserveAttemptAsync(
                order.Id, order.OrderGuid, BnplProvider.Klarna, 12_550, "usd", true, eligibilitySnapshot))
            .ReturnsAsync(reservedAttempt);
        sessionStore.Setup(store => store.CompleteAttemptAsync(reservedAttempt, It.IsAny<Session>()))
            .Returns(Task.CompletedTask);

        var configurationClient = new Mock<IStripeBnplConfigurationClient>();
        configurationClient.Setup(client => client.EnsureProviderOnlyAsync(
                BnplProvider.Klarna, "pmc_test_klarna", false))
            .Returns(Task.CompletedTask);

        SessionCreateOptions capturedOptions = null;
        RequestOptions capturedRequestOptions = null;
        var sessionClient = new Mock<IStripeBnplSessionClient>();
        sessionClient
            .Setup(client => client.CreateAsync(It.IsAny<SessionCreateOptions>(), It.IsAny<RequestOptions>()))
            .Callback<SessionCreateOptions, RequestOptions>((options, requestOptions) =>
            {
                capturedOptions = options;
                capturedRequestOptions = requestOptions;
            })
            .ReturnsAsync(new Session
            {
                Id = "cs_test_klarna",
                Url = "https://checkout.stripe.com/c/pay/cs_test_klarna",
                PaymentIntentId = "pi_test_klarna",
                AmountTotal = returnedAmountMinor,
                Currency = "usd",
                Status = "open",
                ExpiresAt = DateTime.UtcNow.AddMinutes(30)
            });

        var logger = new Mock<ILogger>();
        logger.Setup(service => service.InformationAsync(It.IsAny<string>(), null, null))
            .Returns(Task.CompletedTask);

        var settings = new StripeBnplSettings
        {
            UseSandbox = true,
            TestRestrictedKey = "rk_test_placeholder",
            TestKlarnaConfigurationId = "pmc_test_klarna"
        };

        var service = new StripeBnplCheckoutService(
            addressService.Object,
            eligibilityService.Object,
            Mock.Of<ICountryService>(),
            currencyService.Object,
            orderService.Object,
            productService.Object,
            sessionStore.Object,
            sessionClient.Object,
            configurationClient.Object,
            Mock.Of<IStateProvinceService>(),
            environmentGuard.Object,
            logger.Object,
            webHelper.Object,
            settings);

        return new CheckoutFixture(
            service,
            order,
            orderService,
            sessionClient,
            sessionStore,
            configurationClient,
            () => capturedOptions,
            () => capturedRequestOptions);
    }

    private static StripeBnplEligibilitySnapshotEnvelope CreateEligibilitySnapshot(Order order,
        IList<OrderItem> orderItems) => StripeBnplEligibilitySnapshotCodec.Create(
        new StripeBnplEligibilitySnapshot(
            StripeBnplEligibilitySnapshotCodec.CurrentVersion,
            order.Id,
            order.OrderGuid,
            BnplProvider.Klarna,
            "US",
            "usd",
            12_550,
            orderItems.Select(item => new StripeBnplEligibilitySnapshotItem(
                item.Id,
                item.ProductId,
                item.Quantity,
                item.AttributesXml ?? string.Empty,
                900 + item.ProductId,
                BnplEligibilityState.Allowed,
                "Approved test product",
                7,
                null,
                "unit-test-v1",
                DateTime.UtcNow,
                null)).ToArray()));

    private sealed record CheckoutFixture(
        StripeBnplCheckoutService Service,
        Order Order,
        Mock<IOrderService> OrderService,
        Mock<IStripeBnplSessionClient> SessionClient,
        Mock<IStripeBnplCheckoutSessionStore> SessionStore,
        Mock<IStripeBnplConfigurationClient> ConfigurationClient,
        Func<SessionCreateOptions> GetCapturedOptions,
        Func<RequestOptions> GetCapturedRequestOptions)
    {
        public SessionCreateOptions CapturedOptions => GetCapturedOptions();
        public RequestOptions CapturedRequestOptions => GetCapturedRequestOptions();
    }
}
