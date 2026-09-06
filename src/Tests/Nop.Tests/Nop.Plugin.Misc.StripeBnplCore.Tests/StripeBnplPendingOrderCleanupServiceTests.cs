using Moq;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Misc.StripeBnplCore;
using Nop.Plugin.Misc.StripeBnplCore.Domain;
using Nop.Plugin.Misc.StripeBnplCore.Services;
using Nop.Services.Logging;
using Nop.Services.Orders;
using NUnit.Framework;

namespace Nop.Tests.Nop.Plugin.Misc.StripeBnplCore.Tests;

[TestFixture]
public class StripeBnplPendingOrderCleanupServiceTests
{
    [Test]
    public async Task ExpiredSessionCancelsPendingOrderAndVoidsPayment()
    {
        var order = PendingOrder();
        var session = Session("expired");
        var fixture = CreateFixture(order, session, session);

        var result = await fixture.Service.TryCancelAsync(order.Id, BnplProvider.Klarna,
            "checkout.session.expired", "webhook", session);

        Assert.That(result, Is.True);
        Assert.That(order.PaymentStatus, Is.EqualTo(PaymentStatus.Voided));
        fixture.OrderService.Verify(service => service.UpdateOrderAsync(order), Times.Once);
        fixture.Processing.Verify(service => service.CancelOrderAsync(order, true), Times.Once);
        fixture.OrderService.Verify(service => service.InsertOrderNoteAsync(It.Is<OrderNote>(note =>
            note.OrderId == order.Id && note.Note.Contains(session.SessionId, StringComparison.Ordinal))), Times.Once);
    }

    [Test]
    public async Task PaidOrderIsNeverCancelled()
    {
        var order = PendingOrder();
        order.PaymentStatus = PaymentStatus.Paid;
        var fixture = CreateFixture(order, Session("expired"));

        var result = await fixture.Service.TryCancelAsync(order.Id, BnplProvider.Klarna,
            "checkout.session.expired", "scheduled_task");

        Assert.That(result, Is.False);
        fixture.Processing.Verify(service => service.CancelOrderAsync(It.IsAny<Order>(), It.IsAny<bool>()), Times.Never);
    }

    [Test]
    public async Task OpenUnexpiredSessionIsNotCancelled()
    {
        var order = PendingOrder();
        var session = Session("open");
        session.ExpiresOnUtc = DateTime.UtcNow.AddHours(1);
        var fixture = CreateFixture(order, session);

        var result = await fixture.Service.TryCancelAsync(order.Id, BnplProvider.Klarna,
            "scheduled_cleanup", "scheduled_task");

        Assert.That(result, Is.False);
        fixture.Processing.Verify(service => service.CancelOrderAsync(It.IsAny<Order>(), It.IsAny<bool>()), Times.Never);
    }

    [Test]
    public async Task MissingSessionOlderThanWindowIsCancelled()
    {
        var order = PendingOrder();
        order.CreatedOnUtc = DateTime.UtcNow.AddHours(-25);
        var fixture = CreateFixture(order, null, null);

        var result = await fixture.Service.TryCancelAsync(order.Id, BnplProvider.Klarna,
            "scheduled_cleanup", "scheduled_task");

        Assert.That(result, Is.True);
        Assert.That(order.PaymentStatus, Is.EqualTo(PaymentStatus.Voided));
        fixture.Processing.Verify(service => service.CancelOrderAsync(order, true), Times.Once);
    }

    [Test]
    public async Task MissingSessionWithinWindowIsNotCancelled()
    {
        var order = PendingOrder();
        order.CreatedOnUtc = DateTime.UtcNow.AddHours(-23);
        var fixture = CreateFixture(order, null, null);

        var result = await fixture.Service.TryCancelAsync(order.Id, BnplProvider.Klarna,
            "scheduled_cleanup", "scheduled_task");

        Assert.That(result, Is.False);
        fixture.Processing.Verify(service => service.CancelOrderAsync(It.IsAny<Order>(), It.IsAny<bool>()), Times.Never);
    }

    [Test]
    public async Task NewerOpenRetryPreventsStaleTerminalCancellation()
    {
        var order = PendingOrder();
        var expired = Session("expired");
        var retry = Session("open");
        retry.SessionId = "cs_retry";
        retry.ExpiresOnUtc = DateTime.UtcNow.AddHours(1);
        var fixture = CreateFixture(order, expired, retry);

        var result = await fixture.Service.TryCancelAsync(order.Id, BnplProvider.Klarna,
            "checkout.session.expired", "webhook", expired);

        Assert.That(result, Is.False);
        fixture.Processing.Verify(service => service.CancelOrderAsync(It.IsAny<Order>(), It.IsAny<bool>()), Times.Never);
    }

    [Test]
    public async Task ConcurrentPaymentSuccessProtectsOrderBeforeCancel()
    {
        var pending = PendingOrder();
        var paid = PendingOrder();
        paid.PaymentStatus = PaymentStatus.Paid;
        var session = Session("expired");
        var orderService = new Mock<IOrderService>();
        orderService.SetupSequence(service => service.GetOrderByIdAsync(pending.Id))
            .ReturnsAsync(pending)
            .ReturnsAsync(paid);
        orderService.Setup(service => service.GetOrderNotesByOrderIdAsync(pending.Id, null))
            .ReturnsAsync(new List<OrderNote>());
        var fixture = CreateFixture(orderService, session, session);

        var result = await fixture.Service.TryCancelAsync(pending.Id, BnplProvider.Klarna,
            "checkout.session.expired", "webhook", session);

        Assert.That(result, Is.False);
        fixture.Processing.Verify(service => service.CancelOrderAsync(It.IsAny<Order>(), It.IsAny<bool>()), Times.Never);
    }

    private static Order PendingOrder() => new()
    {
        Id = 852,
        CustomOrderNumber = "852",
        PaymentMethodSystemName = StripeBnplDefaults.GetSystemName(BnplProvider.Klarna),
        PaymentStatus = PaymentStatus.Pending,
        OrderStatus = OrderStatus.Pending,
        CreatedOnUtc = DateTime.UtcNow.AddHours(-1)
    };

    private static StripeBnplCheckoutSession Session(string status) => new()
    {
        OrderId = 852,
        Provider = BnplProvider.Klarna,
        SessionId = "cs_expired",
        Status = status,
        ExpiresOnUtc = DateTime.UtcNow.AddHours(-1),
        UpdatedOnUtc = DateTime.UtcNow
    };

    private static Fixture CreateFixture(Order order, StripeBnplCheckoutSession first,
        StripeBnplCheckoutSession second = null)
    {
        var orderService = new Mock<IOrderService>();
        orderService.Setup(service => service.GetOrderByIdAsync(order.Id)).ReturnsAsync(order);
        orderService.Setup(service => service.GetOrderNotesByOrderIdAsync(order.Id, null))
            .ReturnsAsync(new List<OrderNote>());
        return CreateFixture(orderService, first, second);
    }

    private static Fixture CreateFixture(Mock<IOrderService> orderService,
        StripeBnplCheckoutSession first, StripeBnplCheckoutSession second = null)
    {
        var sessions = new Mock<IStripeBnplCheckoutSessionStore>();
        sessions.SetupSequence(store => store.GetLatestAsync(852, BnplProvider.Klarna))
            .ReturnsAsync(first)
            .ReturnsAsync(second ?? first);
        var processing = new Mock<IOrderProcessingService>();
        processing.Setup(service => service.CancelOrderAsync(It.IsAny<Order>(), true))
            .Returns(Task.CompletedTask);
        var logger = new Mock<ILogger>();
        logger.Setup(service => service.InformationAsync(It.IsAny<string>(), null, null))
            .Returns(Task.CompletedTask);
        logger.Setup(service => service.ErrorAsync(It.IsAny<string>(), It.IsAny<Exception>(), null))
            .Returns(Task.CompletedTask);
        var service = new StripeBnplPendingOrderCleanupService(orderService.Object, processing.Object,
            sessions.Object, logger.Object, new StripeBnplSettings { PendingOrderCancellationHours = 24 });
        return new Fixture(service, orderService, processing, sessions);
    }

    private sealed record Fixture(
        StripeBnplPendingOrderCleanupService Service,
        Mock<IOrderService> OrderService,
        Mock<IOrderProcessingService> Processing,
        Mock<IStripeBnplCheckoutSessionStore> SessionStore);
}
