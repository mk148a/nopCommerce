using Moq;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Misc.StripeBnplCore;
using Nop.Plugin.Misc.StripeBnplCore.Domain;
using Nop.Plugin.Misc.StripeBnplCore.Services;
using Nop.Services.Customers;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Payments;
using NUnit.Framework;
using Stripe.Checkout;

public class IncidentRegressionTests
{
    private static (BnplEligibilityService Service, List<ShoppingCartItem> Cart) Eligibility(string country, string currency, string account = "US")
    {
        var cart = new List<ShoppingCartItem> { new() { ProductId = 42, CustomerId = 5, StoreId = 1 } };
        var context = new Mock<IBnplCartContextService>();
        context.Setup(x => x.GetAsync(It.IsAny<IList<ShoppingCartItem>>())).ReturnsAsync(new BnplCartContext(country, currency, 439.10m));
        context.Setup(x => x.GetAsync(It.IsAny<Order>())).ReturnsAsync(new BnplCartContext(country, currency, 439.10m));
        var store = new Mock<IBnplProductEligibilityStore>();
        store.Setup(x => x.GetByProductIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), BnplProvider.Affirm))
            .ReturnsAsync(new[] { new BnplProductEligibility { ProductId = 42, EligibilityState = BnplEligibilityState.Allowed } });
        var guard = new Mock<IStripeBnplEnvironmentGuard>();
        guard.Setup(x => x.Check()).Returns(new BnplEnvironmentGuardResult(true, "test", ""));
        var settings = new StripeBnplSettings { UseSandbox = true, TestRestrictedKey = "test-placeholder", TestAffirmConfigurationId = "test", StripeAccountCountryIso2 = account };
        return (new BnplEligibilityService(Mock.Of<IBnplCatalogRiskService>(), context.Object, store.Object, guard.Object, settings), cart);
    }

    [TestCase("RO", "EUR", "US", false)]
    [TestCase("RO", "USD", "US", false)]
    [TestCase("US", "EUR", "US", false)]
    [TestCase("US", "CAD", "US", false)]
    [TestCase("US", "USD", "US", true)]
    [TestCase("CA", "CAD", "CA", true)]
    [TestCase("GB", "GBP", "GB", false)]
    public async Task AffirmRejectsUnsupportedCartAndOrderBeforeSessionCreation(string country, string currency, string account, bool expected)
    {
        var f = Eligibility(country, currency, account);
        Assert.That((await f.Service.EvaluateCartAsync(BnplProvider.Affirm, f.Cart)).IsEligible, Is.EqualTo(expected));
        Assert.That((await f.Service.EvaluateOrderAsync(BnplProvider.Affirm, new Order(), new List<OrderItem> { new() { ProductId = 42 } })).IsEligible, Is.EqualTo(expected));
    }

    [TestCase("RO", "EUR", false)]
    [TestCase("US", "USD", true)]
    public async Task ProcessPaymentRevalidatesStaleSelectionBeforeOrderIsSaved(string country, string currency, bool expectedSuccess)
    {
        var f = Eligibility(country, currency);
        var customers = new Mock<ICustomerService>();
        customers.Setup(x => x.GetCustomerByIdAsync(5)).ReturnsAsync(new Customer { Id = 5 });
        var carts = new Mock<IShoppingCartService>();
        carts.Setup(x => x.GetShoppingCartAsync(It.IsAny<Customer>(), ShoppingCartType.ShoppingCart, 1, null, null, null)).ReturnsAsync(f.Cart);
        var checkout = new Mock<IStripeBnplCheckoutService>();
        var service = new StripeBnplService(f.Service, null, checkout.Object, null, new StripeBnplSettings(), null, customers.Object, carts.Object);
        var method = new Nop.Plugin.Payments.StripeAffirm.StripeAffirmPaymentMethod(null, service);
        var result = await method.ProcessPaymentAsync(new ProcessPaymentRequest { CustomerId = 5, StoreId = 1 });
        Assert.That(result.Success, Is.EqualTo(expectedSuccess));
        checkout.Verify(x => x.CreateAsync(It.IsAny<BnplProvider>(), It.IsAny<Order>()), Times.Never);
        if (!expectedSuccess) Assert.That(result.Errors.Single(), Does.Contain("choose another payment method"));
    }

    [TestCase("creating")]
    [TestCase("create_unknown")]
    [TestCase("create_expired")]
    [TestCase("complete")]
    public async Task CleanupNeverCancelsUnresolvedOrProcessingAttempt(string status)
    {
        var f = Cleanup(status, "pending:key");
        Assert.That(await f.Service.TryCancelAsync(42, BnplProvider.Affirm, "scheduled_cleanup", "scheduled_task"), Is.False);
        f.Processing.Verify(x => x.CancelOrderAsync(It.IsAny<Order>(), true), Times.Never);
    }

    [TestCase("complete", "paid", false)]
    [TestCase("complete", "unpaid", false)]
    [TestCase("open", "unpaid", false)]
    [TestCase("expired", "unpaid", true)]
    public async Task CleanupRequiresRemoteExpiryAndPreservesPaidOrder(string remoteStatus, string paymentStatus, bool cancel)
    {
        var f = Cleanup("open", "cs_test");
        f.Client.Setup(x => x.GetAsync("cs_test", It.IsAny<SessionGetOptions>())).ReturnsAsync(new Session
        { Id = "cs_test", Status = remoteStatus, PaymentStatus = paymentStatus, Livemode = false,
          AmountTotal = 43910, Currency = "usd", Metadata = new() { ["order_id"] = "42", ["order_guid"] = f.Order.OrderGuid.ToString("D"), ["eligibility_snapshot_hash"] = "hash" } });
        Assert.That(await f.Service.TryCancelAsync(42, BnplProvider.Affirm, "scheduled_cleanup", "scheduled_task"), Is.EqualTo(cancel));
        f.Processing.Verify(x => x.CancelOrderAsync(It.IsAny<Order>(), true), cancel ? Times.Once() : Times.Never());
        f.Finalizer.Verify(x => x.FinalizeSessionAsync("cs_test", f.Order.OrderGuid, "scheduled_cleanup_reconciliation"), paymentStatus == "paid" ? Times.Once() : Times.Never());
    }

    [Test]
    public void CleanupApiFailureDoesNotVoidOrCancel()
    {
        var f = Cleanup("open", "cs_test");
        f.Client.Setup(x => x.GetAsync("cs_test", It.IsAny<SessionGetOptions>())).ThrowsAsync(new HttpRequestException());
        Assert.ThrowsAsync<HttpRequestException>(async () => await f.Service.TryCancelAsync(42, BnplProvider.Affirm, "scheduled_cleanup", "scheduled_task"));
        Assert.That(f.Order.PaymentStatus, Is.EqualTo(PaymentStatus.Pending));
        f.Processing.Verify(x => x.CancelOrderAsync(It.IsAny<Order>(), true), Times.Never);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task CleanupDefersProcessingOrMismatchedRemoteSession(bool processing)
    {
        var f = Cleanup("open", "cs_test");
        var remote = new Session { Id = "cs_test", Status = "expired", PaymentStatus = "unpaid", Livemode = false,
            AmountTotal = 43910, Currency = processing ? "usd" : "eur", Metadata = new()
            { ["order_id"] = "42", ["order_guid"] = f.Order.OrderGuid.ToString("D"), ["eligibility_snapshot_hash"] = "hash" } };
        if (processing) remote.PaymentIntent = new Stripe.PaymentIntent { Id = "pi_processing", Status = "processing" };
        f.Client.Setup(x => x.GetAsync("cs_test", It.IsAny<SessionGetOptions>())).ReturnsAsync(remote);
        if (processing) Assert.That(await f.Service.TryCancelAsync(42, BnplProvider.Affirm, "scheduled_cleanup", "scheduled_task"), Is.False);
        else Assert.ThrowsAsync<InvalidOperationException>(async () => await f.Service.TryCancelAsync(42, BnplProvider.Affirm, "scheduled_cleanup", "scheduled_task"));
        f.Processing.Verify(x => x.CancelOrderAsync(It.IsAny<Order>(), true), Times.Never);
    }

    [Test]
    public async Task PaidCleanupReleasesOrderGateBeforeCallingFinalizer()
    {
        var f = Cleanup("open", "cs_test");
        f.Client.Setup(x => x.GetAsync("cs_test", It.IsAny<SessionGetOptions>())).ReturnsAsync(new Session
        { Id = "cs_test", Status = "complete", PaymentStatus = "paid", Livemode = false,
          AmountTotal = 43910, Currency = "usd", Metadata = new() { ["order_id"] = "42", ["order_guid"] = f.Order.OrderGuid.ToString("D"), ["eligibility_snapshot_hash"] = "hash" } });
        f.Finalizer.Setup(x => x.FinalizeSessionAsync("cs_test", f.Order.OrderGuid, "scheduled_cleanup_reconciliation"))
            .Returns(async () =>
            {
                var type = typeof(StripeBnplService).Assembly.GetType("Nop.Plugin.Misc.StripeBnplCore.Services.StripeBnplOrderGate");
                var gate = (SemaphoreSlim)type.GetMethod("Get", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, new object[] { 42 });
                Assert.That(await gate.WaitAsync(TimeSpan.FromSeconds(1)), Is.True, "Finalizer must be able to acquire the shared order gate.");
                gate.Release();
                return new StripeBnplFinalizationResult(42, true, false, "paid", "test");
            });
        Assert.That(await f.Service.TryCancelAsync(42, BnplProvider.Affirm, "scheduled_cleanup", "scheduled_task"), Is.False);
    }

    private static CleanupFixture Cleanup(string status, string id)
    {
        var order = new Order { Id = 42, OrderGuid = Guid.NewGuid(), PaymentMethodSystemName = "Payments.StripeAffirm", PaymentStatus = PaymentStatus.Pending, OrderStatus = OrderStatus.Pending, CreatedOnUtc = DateTime.UtcNow.AddDays(-2) };
        var session = new StripeBnplCheckoutSession { Id = 9, OrderId = 42, Provider = BnplProvider.Affirm, SessionId = id, Status = status, AmountMinor = 43910, Currency = "usd", IsSandbox = true, EligibilitySnapshotHash = "hash", ExpiresOnUtc = DateTime.UtcNow.AddDays(-1), UpdatedOnUtc = DateTime.UtcNow.AddDays(-1) };
        var orders = new Mock<IOrderService>(); orders.Setup(x => x.GetOrderByIdAsync(42)).ReturnsAsync(order);
        orders.Setup(x => x.GetOrderNotesByOrderIdAsync(42, null)).ReturnsAsync(new List<OrderNote>());
        var sessions = new Mock<IStripeBnplCheckoutSessionStore>(); sessions.Setup(x => x.GetLatestAsync(42, BnplProvider.Affirm)).ReturnsAsync(session);
        var processing = new Mock<IOrderProcessingService>(); var client = new Mock<IStripeBnplSessionClient>(); var finalizer = new Mock<IStripeBnplPaymentFinalizer>();
        return new(new StripeBnplPendingOrderCleanupService(orders.Object, processing.Object, sessions.Object, Mock.Of<ILogger>(), new StripeBnplSettings(), client.Object, finalizer.Object), order, processing, client, finalizer);
    }
    private sealed record CleanupFixture(StripeBnplPendingOrderCleanupService Service, Order Order, Mock<IOrderProcessingService> Processing, Mock<IStripeBnplSessionClient> Client, Mock<IStripeBnplPaymentFinalizer> Finalizer);
}
