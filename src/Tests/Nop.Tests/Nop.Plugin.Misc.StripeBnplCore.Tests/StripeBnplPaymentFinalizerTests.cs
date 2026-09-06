using Moq;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Misc.StripeBnplCore.Domain;
using Nop.Plugin.Misc.StripeBnplCore.Services;
using Nop.Services.Directory;
using Nop.Services.Logging;
using Nop.Services.Orders;
using NUnit.Framework;
using Stripe;
using Stripe.Checkout;

namespace Nop.Tests.Nop.Plugin.Misc.StripeBnplCore.Tests;

[TestFixture]
public class StripeBnplPaymentFinalizerTests
{
    [Test]
    public async Task ConcurrentWebhookAndReturnFinalizeOrderExactlyOnce()
    {
        var fixture = CreateFixture();

        var results = await Task.WhenAll(
            fixture.Finalizer.FinalizeSessionAsync(fixture.StripeSession.Id, fixture.Order.OrderGuid, "webhook"),
            fixture.Finalizer.FinalizeSessionAsync(fixture.StripeSession.Id, fixture.Order.OrderGuid, "return"));

        Assert.Multiple(() =>
        {
            Assert.That(results.All(result => result.IsPaid), Is.True);
            Assert.That(results.Count(result => result.AlreadyPaid), Is.EqualTo(1));
            Assert.That(results.Count(result => !result.AlreadyPaid), Is.EqualTo(1));
            Assert.That(fixture.Order.PaymentStatus, Is.EqualTo(PaymentStatus.Paid));
        });
        fixture.OrderProcessingService.Verify(service => service.MarkOrderAsPaidAsync(fixture.Order), Times.Once);
        fixture.OrderService.Verify(service => service.UpdateOrderAsync(fixture.Order), Times.Once);
        fixture.OrderService.Verify(service => service.InsertOrderNoteAsync(It.IsAny<OrderNote>()), Times.Once);
    }

    [Test]
    public async Task UnpaidEventDoesNotConvertAndLaterPaidEventCanFinalize()
    {
        var fixture = CreateFixture();
        fixture.StripeSession.PaymentStatus = "unpaid";
        fixture.StripeSession.Status = "open";

        var unpaid = await fixture.Finalizer.FinalizeSessionAsync(fixture.StripeSession.Id);

        Assert.Multiple(() =>
        {
            Assert.That(unpaid.IsPaid, Is.False);
            Assert.That(fixture.Order.PaymentStatus, Is.EqualTo(PaymentStatus.Pending));
        });
        fixture.PaymentClient.Verify(client => client.GetPaymentIntentAsync(It.IsAny<string>()), Times.Never);
        fixture.OrderProcessingService.Verify(service => service.MarkOrderAsPaidAsync(It.IsAny<Order>()), Times.Never);
        fixture.ConversionService.Verify(service => service.EnsureReadyAsync(
            It.IsAny<Order>(), It.IsAny<BnplProvider>(), It.IsAny<string>(), It.IsAny<decimal?>()), Times.Never);

        fixture.StripeSession.PaymentStatus = "paid";
        fixture.StripeSession.Status = "complete";
        var paid = await fixture.Finalizer.FinalizeSessionAsync(fixture.StripeSession.Id);

        Assert.That(paid.IsPaid, Is.True);
        fixture.OrderProcessingService.Verify(service => service.MarkOrderAsPaidAsync(fixture.Order), Times.Once);
        fixture.ConversionService.Verify(service => service.EnsureReadyAsync(
            fixture.Order, BnplProvider.Klarna, "klarna", 125.50m), Times.Once);
    }

    [Test]
    public async Task LateUnpaidEventCannotDowngradeAlreadyPaidOrder()
    {
        var fixture = CreateFixture();
        await fixture.Finalizer.FinalizeSessionAsync(fixture.StripeSession.Id);
        fixture.StripeSession.PaymentStatus = "unpaid";
        fixture.StripeSession.Status = "expired";

        var late = await fixture.Finalizer.FinalizeSessionAsync(fixture.StripeSession.Id);

        Assert.Multiple(() =>
        {
            Assert.That(late.IsPaid, Is.False);
            Assert.That(fixture.Order.PaymentStatus, Is.EqualTo(PaymentStatus.Paid));
        });
        fixture.OrderProcessingService.Verify(service => service.MarkOrderAsPaidAsync(fixture.Order), Times.Once);
    }

    [Test]
    public void LatePaidEventCannotReopenTerminalOrder()
    {
        var fixture = CreateFixture();
        fixture.Order.OrderStatus = OrderStatus.Cancelled;
        fixture.Order.PaymentStatus = PaymentStatus.Voided;

        var exception = Assert.ThrowsAsync<NopException>(async () =>
            await fixture.Finalizer.FinalizeSessionAsync(fixture.StripeSession.Id));

        Assert.That(exception.Message, Does.Contain("terminal"));
        fixture.OrderProcessingService.Verify(service => service.MarkOrderAsPaidAsync(It.IsAny<Order>()), Times.Never);
        fixture.ConversionService.Verify(service => service.EnsureReadyAsync(
            It.IsAny<Order>(), It.IsAny<BnplProvider>(), It.IsAny<string>(), It.IsAny<decimal?>()), Times.Never);
    }

    [Test]
    public void ActualPaymentMethodMustMatchSelectedProvider()
    {
        var fixture = CreateFixture();
        fixture.PaymentIntent.PaymentMethod = new PaymentMethod { Type = "affirm" };

        var exception = Assert.ThrowsAsync<NopException>(async () =>
            await fixture.Finalizer.FinalizeSessionAsync(fixture.StripeSession.Id));

        Assert.That(exception.Message, Does.Contain("does not match"));
        fixture.OrderProcessingService.Verify(service => service.MarkOrderAsPaidAsync(It.IsAny<Order>()), Times.Never);
    }

    [Test]
    public void TamperedEligibilityEvidenceCannotReachPaidTransition()
    {
        var fixture = CreateFixture();
        fixture.InternalSession.EligibilitySnapshotJson = fixture.InternalSession.EligibilitySnapshotJson.Replace(
            "unit-test-v1", "tampered-policy", StringComparison.Ordinal);

        var exception = Assert.ThrowsAsync<NopException>(async () =>
            await fixture.Finalizer.FinalizeSessionAsync(fixture.StripeSession.Id));

        Assert.That(exception.Message, Does.Contain("integrity"));
        fixture.OrderProcessingService.Verify(service => service.MarkOrderAsPaidAsync(It.IsAny<Order>()), Times.Never);
        fixture.PaymentRecordStore.Verify(store => store.TryAcquireFinalizationAsync(
            It.IsAny<StripeBnplPaymentSnapshot>()), Times.Never);
    }

    [Test]
    public async Task MissingBalanceTransactionIsRecordedAsPendingRatherThanZeroFee()
    {
        var fixture = CreateFixture();
        fixture.PaymentIntent.LatestCharge.BalanceTransaction = null;

        await fixture.Finalizer.FinalizeSessionAsync(fixture.StripeSession.Id);

        fixture.PaymentRecordStore.Verify(store => store.CompleteFinalizationAsync(
            It.Is<StripeBnplPaymentSnapshot>(snapshot =>
                snapshot.FeeDataStatus == StripeBnplFeeDataStatus.Pending &&
                snapshot.FeeMinor == null &&
                snapshot.NetMinor == null &&
                snapshot.BalanceTransactionId == null)), Times.Once);
    }

    [Test]
    public async Task ExpandedBalanceTransactionIsRecordedAsCompleteFeeData()
    {
        var fixture = CreateFixture();

        await fixture.Finalizer.FinalizeSessionAsync(fixture.StripeSession.Id);

        fixture.PaymentRecordStore.Verify(store => store.CompleteFinalizationAsync(
            It.Is<StripeBnplPaymentSnapshot>(snapshot =>
                snapshot.FeeDataStatus == StripeBnplFeeDataStatus.Complete &&
                snapshot.FeeMinor == 850 &&
                snapshot.NetMinor == 11_700 &&
                snapshot.BalanceTransactionId == "txn_test_finalizer")), Times.Once);
    }

    [TestCase("refunded", "paid", "refunded")]
    [TestCase("partially_refunded", "paid", "partially_refunded")]
    [TestCase("disputed_needs_response", "paid", "disputed_needs_response")]
    [TestCase("finalizing", "paid", "paid")]
    public void FinalizationCannotDowngradeAConcurrentRefundOrDispute(string current, string next,
        string expected)
    {
        Assert.That(StripeBnplPaymentRecordStore.SelectMonotonicLifecycleStatus(current, next),
            Is.EqualTo(expected));
    }

    private static FinalizerFixture CreateFixture()
    {
        var order = new Order
        {
            Id = 42,
            OrderGuid = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            CustomOrderNumber = "HOOD-42",
            PaymentMethodSystemName = "Payments.StripeKlarna",
            PaymentStatus = PaymentStatus.Pending,
            OrderStatus = OrderStatus.Pending,
            CustomerCurrencyCode = "USD",
            CurrencyRate = 1m,
            OrderTotal = 125.50m
        };
        var internalSession = new StripeBnplCheckoutSession
        {
            OrderId = order.Id,
            Provider = BnplProvider.Klarna,
            SessionId = "cs_test_finalizer",
            PaymentIntentId = "pi_test_finalizer",
            AmountMinor = 12_550,
            Currency = "usd",
            Status = "open",
            IsSandbox = true
        };
        var orderItems = new List<OrderItem>
        {
            new()
            {
                Id = 101,
                OrderId = order.Id,
                ProductId = 501,
                Quantity = 1,
                AttributesXml = "<Attributes />"
            }
        };
        var eligibilitySnapshot = StripeBnplEligibilitySnapshotCodec.Create(
            new StripeBnplEligibilitySnapshot(
                StripeBnplEligibilitySnapshotCodec.CurrentVersion,
                order.Id,
                order.OrderGuid,
                BnplProvider.Klarna,
                "US",
                "usd",
                12_550,
                new[]
                {
                    new StripeBnplEligibilitySnapshotItem(101, 501, 1, "<Attributes />", 701,
                        BnplEligibilityState.Allowed, "Approved", 7, null, "unit-test-v1",
                        DateTime.UtcNow, null)
                }));
        internalSession.EligibilitySnapshotJson = eligibilitySnapshot.Json;
        internalSession.EligibilitySnapshotHash = eligibilitySnapshot.Sha256;
        var stripeSession = new Session
        {
            Id = internalSession.SessionId,
            PaymentIntentId = internalSession.PaymentIntentId,
            AmountTotal = 12_550,
            Currency = "usd",
            Status = "complete",
            PaymentStatus = "paid",
            Livemode = false,
            Metadata = new Dictionary<string, string>
            {
                ["order_id"] = order.Id.ToString(),
                ["order_guid"] = order.OrderGuid.ToString("D"),
                ["provider"] = "klarna",
                ["eligibility_snapshot_hash"] = eligibilitySnapshot.Sha256
            }
        };
        var paymentIntent = new PaymentIntent
        {
            Id = internalSession.PaymentIntentId,
            Amount = 12_550,
            Currency = "usd",
            Status = "succeeded",
            PaymentMethod = new PaymentMethod { Type = "klarna" },
            LatestChargeId = "ch_test_finalizer",
            LatestCharge = new Charge
            {
                Id = "ch_test_finalizer",
                BalanceTransaction = new BalanceTransaction
                {
                    Id = "txn_test_finalizer",
                    Fee = 850,
                    Net = 11_700,
                    Currency = "usd",
                    FeeDetails = new List<BalanceTransactionFeeDetail>()
                }
            },
            Metadata = new Dictionary<string, string>
            {
                ["order_id"] = order.Id.ToString(),
                ["order_guid"] = order.OrderGuid.ToString("D"),
                ["eligibility_snapshot_hash"] = eligibilitySnapshot.Sha256
            }
        };

        var currencyService = new Mock<ICurrencyService>();
        currencyService.Setup(service => service.ConvertCurrency(order.OrderTotal, 1m)).Returns(order.OrderTotal);

        var orderService = new Mock<IOrderService>();
        orderService.Setup(service => service.GetOrderByIdAsync(order.Id)).ReturnsAsync(order);
        orderService.Setup(service => service.GetOrderItemsAsync(order.Id, null, null, 0)).ReturnsAsync(orderItems);
        orderService.Setup(service => service.UpdateOrderAsync(order)).Returns(Task.CompletedTask);
        orderService.Setup(service => service.GetOrderNotesByOrderIdAsync(order.Id, null))
            .ReturnsAsync(new List<OrderNote>());
        orderService.Setup(service => service.InsertOrderNoteAsync(It.IsAny<OrderNote>()))
            .Returns(Task.CompletedTask);

        var orderProcessingService = new Mock<IOrderProcessingService>();
        orderProcessingService.Setup(service => service.MarkOrderAsPaidAsync(order))
            .Callback(() =>
            {
                order.PaymentStatus = PaymentStatus.Paid;
                order.OrderStatus = OrderStatus.Processing;
            })
            .Returns(Task.CompletedTask);

        var sessionStore = new Mock<IStripeBnplCheckoutSessionStore>();
        sessionStore.Setup(store => store.GetBySessionIdAsync(internalSession.SessionId))
            .ReturnsAsync(internalSession);
        sessionStore.Setup(store => store.UpdateStatusAsync(
                internalSession, It.IsAny<string>(), It.IsAny<string>()))
            .Callback<StripeBnplCheckoutSession, string, string>((session, status, paymentIntentId) =>
            {
                session.Status = status;
                if (!string.IsNullOrWhiteSpace(paymentIntentId))
                    session.PaymentIntentId = paymentIntentId;
            })
            .Returns(Task.CompletedTask);

        var sessionClient = new Mock<IStripeBnplSessionClient>();
        sessionClient.Setup(client => client.GetAsync(internalSession.SessionId, It.IsAny<SessionGetOptions>()))
            .ReturnsAsync(() => stripeSession);

        var paymentClient = new Mock<IStripeBnplPaymentClient>();
        paymentClient.Setup(client => client.GetPaymentIntentAsync(internalSession.PaymentIntentId))
            .ReturnsAsync(() => paymentIntent);

        var paymentRecordStore = new Mock<IStripeBnplPaymentRecordStore>();
        paymentRecordStore.Setup(store => store.TryAcquireFinalizationAsync(It.IsAny<StripeBnplPaymentSnapshot>()))
            .ReturnsAsync(StripeBnplFinalizationClaim.Acquired);
        paymentRecordStore.Setup(store => store.CompleteFinalizationAsync(It.IsAny<StripeBnplPaymentSnapshot>()))
            .Returns(Task.CompletedTask);

        var conversionService = new Mock<IStripeBnplConversionService>();
        conversionService.Setup(service => service.EnsureReadyAsync(
                order, BnplProvider.Klarna, It.IsAny<string>(), It.IsAny<decimal?>()))
            .ReturnsAsync(new StripeBnplConversionRecord { OrderId = order.Id, Status = "ready" });

        var environmentGuard = new Mock<IStripeBnplEnvironmentGuard>();
        environmentGuard.Setup(guard => guard.EnsureSafe());

        var logger = new Mock<ILogger>();
        logger.Setup(service => service.InformationAsync(It.IsAny<string>(), null, null))
            .Returns(Task.CompletedTask);

        var finalizer = new StripeBnplPaymentFinalizer(
            currencyService.Object,
            orderProcessingService.Object,
            orderService.Object,
            sessionStore.Object,
            conversionService.Object,
            environmentGuard.Object,
            paymentClient.Object,
            paymentRecordStore.Object,
            sessionClient.Object,
            logger.Object);

        return new FinalizerFixture(
            finalizer,
            order,
            internalSession,
            stripeSession,
            paymentIntent,
            orderProcessingService,
            orderService,
            paymentClient,
            conversionService,
            paymentRecordStore);
    }

    private sealed record FinalizerFixture(
        StripeBnplPaymentFinalizer Finalizer,
        Order Order,
        StripeBnplCheckoutSession InternalSession,
        Session StripeSession,
        PaymentIntent PaymentIntent,
        Mock<IOrderProcessingService> OrderProcessingService,
        Mock<IOrderService> OrderService,
        Mock<IStripeBnplPaymentClient> PaymentClient,
        Mock<IStripeBnplConversionService> ConversionService,
        Mock<IStripeBnplPaymentRecordStore> PaymentRecordStore);
}
