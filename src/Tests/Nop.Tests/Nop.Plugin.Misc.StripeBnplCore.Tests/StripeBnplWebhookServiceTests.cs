using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Moq;
using Nop.Plugin.Misc.StripeBnplCore;
using Nop.Plugin.Misc.StripeBnplCore.Domain;
using Nop.Plugin.Misc.StripeBnplCore.Services;
using Nop.Services.Logging;
using Nop.Services.Orders;
using NUnit.Framework;

namespace Nop.Tests.Nop.Plugin.Misc.StripeBnplCore.Tests;

[TestFixture]
public class StripeBnplWebhookServiceTests
{
    private const string SigningSecret = "whsec_unit_test_only";

    [Test]
    public async Task DuplicateStripeEventIsFinalizedExactlyOnce()
    {
        var fixture = CreateFixture();
        fixture.EventStore
            .SetupSequence(store => store.TryBeginAsync(It.IsAny<StripeBnplWebhookEnvelope>()))
            .ReturnsAsync(new StripeBnplWebhookBeginResult(StripeBnplWebhookBeginState.Started, "lease_one"))
            .ReturnsAsync(new StripeBnplWebhookBeginResult(StripeBnplWebhookBeginState.Duplicate, null));
        var payload = BuildEvent("evt_duplicate", "checkout.session.completed", "cs_duplicate", false);
        var signature = Sign(payload);

        await fixture.Service.ProcessAsync(payload, signature);
        await fixture.Service.ProcessAsync(payload, signature);

        fixture.Finalizer.Verify(finalizer => finalizer.FinalizeSessionAsync(
            "cs_duplicate", null, "checkout.session.completed"), Times.Once);
        fixture.EventStore.Verify(store => store.CompleteAsync(
            "evt_duplicate", "lease_one", "processed", 42), Times.Once);
        fixture.EventStore.Verify(store => store.FailAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
    }

    [Test]
    public void InvalidSignatureIsRejectedBeforeIdempotencyOrPaymentWork()
    {
        var fixture = CreateFixture();
        var payload = BuildEvent("evt_bad_signature", "checkout.session.completed", "cs_bad", false);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var exception = Assert.ThrowsAsync<StripeBnplWebhookValidationException>(async () =>
            await fixture.Service.ProcessAsync(payload, $"t={timestamp},v1=invalid"));

        Assert.That(exception.Message, Does.Contain("validation failed"));
        fixture.EventStore.Verify(store => store.TryBeginAsync(It.IsAny<StripeBnplWebhookEnvelope>()), Times.Never);
        fixture.Finalizer.Verify(finalizer => finalizer.FinalizeSessionAsync(
            It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public void LiveEventCannotEnterSandboxWebhookPipeline()
    {
        var fixture = CreateFixture();
        var payload = BuildEvent("evt_live", "checkout.session.completed", "cs_live", true);

        var exception = Assert.ThrowsAsync<StripeBnplWebhookValidationException>(async () =>
            await fixture.Service.ProcessAsync(payload, Sign(payload)));

        Assert.That(exception.Message, Does.Contain("environment"));
        fixture.EventStore.Verify(store => store.TryBeginAsync(It.IsAny<StripeBnplWebhookEnvelope>()), Times.Never);
    }

    [Test]
    public void ActiveWebhookLeaseReturnsRetryableFailureInsteadOfAcknowledgement()
    {
        var fixture = CreateFixture();
        fixture.EventStore.Setup(store => store.TryBeginAsync(It.IsAny<StripeBnplWebhookEnvelope>()))
            .ReturnsAsync(new StripeBnplWebhookBeginResult(StripeBnplWebhookBeginState.InProgress, null));
        var payload = BuildEvent("evt_in_progress", "checkout.session.completed", "cs_in_progress", false);

        var exception = Assert.ThrowsAsync<StripeBnplWebhookRetryException>(async () =>
            await fixture.Service.ProcessAsync(payload, Sign(payload)));

        Assert.That(exception.Message, Does.Contain("retry"));
        fixture.Finalizer.Verify(finalizer => finalizer.FinalizeSessionAsync(
            It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string>()), Times.Never);
        fixture.EventStore.Verify(store => store.CompleteAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>()), Times.Never);
    }

    [Test]
    public void FinalizationClaimInProgressFailsWebhookForStripeRetry()
    {
        var fixture = CreateFixture();
        fixture.Finalizer.Setup(finalizer => finalizer.FinalizeSessionAsync(
                "cs_finalizing", null, "checkout.session.completed"))
            .ReturnsAsync(new StripeBnplFinalizationResult(
                42, false, false, "processing", "Another worker is finalizing this payment."));
        var payload = BuildEvent("evt_finalizing", "checkout.session.completed", "cs_finalizing", false);

        var exception = Assert.ThrowsAsync<StripeBnplWebhookRetryException>(async () =>
            await fixture.Service.ProcessAsync(payload, Sign(payload)));

        Assert.That(exception.Message, Does.Contain("retry"));
        fixture.EventStore.Verify(store => store.FailAsync(
            "evt_finalizing", "lease_default", It.IsAny<StripeBnplWebhookRetryException>()), Times.Once);
        fixture.EventStore.Verify(store => store.CompleteAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>()), Times.Never);
    }

    [Test]
    public async Task LateExpiredEventCannotDowngradeCompletedPaidSession()
    {
        var internalSession = new StripeBnplCheckoutSession
        {
            OrderId = 42,
            Provider = BnplProvider.Klarna,
            SessionId = "cs_out_of_order",
            PaymentIntentId = "pi_out_of_order",
            Status = "open",
            IsSandbox = true
        };
        var fixture = CreateFixture(internalSession);
        fixture.EventStore
            .Setup(store => store.TryBeginAsync(It.IsAny<StripeBnplWebhookEnvelope>()))
            .ReturnsAsync(new StripeBnplWebhookBeginResult(StripeBnplWebhookBeginState.Started, "lease_ooo"));
        fixture.Finalizer
            .Setup(finalizer => finalizer.FinalizeSessionAsync(
                internalSession.SessionId, null, "checkout.session.completed"))
            .Callback(() => internalSession.Status = "paid")
            .ReturnsAsync(new StripeBnplFinalizationResult(42, true, false, "paid", "Order marked paid."));
        var completed = BuildEvent("evt_completed", "checkout.session.completed", internalSession.SessionId, false);
        var expired = BuildEvent("evt_expired", "checkout.session.expired", internalSession.SessionId, false);

        await fixture.Service.ProcessAsync(completed, Sign(completed));
        await fixture.Service.ProcessAsync(expired, Sign(expired));

        Assert.That(internalSession.Status, Is.EqualTo("paid"));
        fixture.SessionStore.Verify(store => store.UpdateStatusAsync(
            internalSession, It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        fixture.EventStore.Verify(store => store.CompleteAsync(
            "evt_expired", "lease_ooo", "ignored", 42), Times.Once);
    }

    [Test]
    public async Task TerminalSessionEventInvokesPendingOrderCleanupBeforeAcknowledgement()
    {
        var internalSession = new StripeBnplCheckoutSession
        {
            OrderId = 42,
            Provider = BnplProvider.Klarna,
            SessionId = "cs_terminal_cleanup",
            Status = "open",
            IsSandbox = true
        };
        var fixture = CreateFixture(internalSession);
        fixture.PendingCleanup
            .Setup(service => service.TryCancelAsync(42, BnplProvider.Klarna,
                "checkout.session.expired", "webhook", internalSession))
            .ReturnsAsync(true);
        var payload = BuildEvent("evt_terminal_cleanup", "checkout.session.expired",
            internalSession.SessionId, false);

        await fixture.Service.ProcessAsync(payload, Sign(payload));

        fixture.PendingCleanup.Verify(service => service.TryCancelAsync(42, BnplProvider.Klarna,
            "checkout.session.expired", "webhook", internalSession), Times.Once);
        fixture.EventStore.Verify(store => store.CompleteAsync(
            "evt_terminal_cleanup", "lease_default", "processed", 42), Times.Once);
    }

    [Test]
    public void TerminalCleanupFailureLeavesWebhookForStripeRetry()
    {
        var internalSession = new StripeBnplCheckoutSession
        {
            OrderId = 42,
            Provider = BnplProvider.Klarna,
            SessionId = "cs_terminal_retry",
            Status = "open",
            IsSandbox = true
        };
        var fixture = CreateFixture(internalSession);
        fixture.PendingCleanup
            .Setup(service => service.TryCancelAsync(It.IsAny<int>(), It.IsAny<BnplProvider>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<StripeBnplCheckoutSession>()))
            .ThrowsAsync(new InvalidOperationException("cancel failed"));
        var payload = BuildEvent("evt_terminal_retry", "checkout.session.expired",
            internalSession.SessionId, false);

        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await fixture.Service.ProcessAsync(payload, Sign(payload)));

        Assert.That(exception.Message, Is.EqualTo("cancel failed"));
        fixture.EventStore.Verify(store => store.FailAsync(
            "evt_terminal_retry", "lease_default", It.IsAny<Exception>()), Times.Once);
        fixture.EventStore.Verify(store => store.CompleteAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>()), Times.Never);
    }

    private static WebhookFixture CreateFixture(StripeBnplCheckoutSession internalSession = null)
    {
        var finalizer = new Mock<IStripeBnplPaymentFinalizer>();
        finalizer
            .Setup(service => service.FinalizeSessionAsync(It.IsAny<string>(), null, It.IsAny<string>()))
            .ReturnsAsync(new StripeBnplFinalizationResult(42, true, false, "paid", "Order marked paid."));

        var sessionStore = new Mock<IStripeBnplCheckoutSessionStore>();
        if (internalSession != null)
        {
            sessionStore.Setup(store => store.GetBySessionIdAsync(internalSession.SessionId))
                .ReturnsAsync(internalSession);
        }

        var eventStore = new Mock<IStripeBnplWebhookEventStore>();
        eventStore.Setup(store => store.TryBeginAsync(It.IsAny<StripeBnplWebhookEnvelope>()))
            .ReturnsAsync(new StripeBnplWebhookBeginResult(StripeBnplWebhookBeginState.Started, "lease_default"));
        eventStore.Setup(store => store.CompleteAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>()))
            .Returns(Task.CompletedTask);
        eventStore.Setup(store => store.FailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Exception>()))
            .Returns(Task.CompletedTask);

        var logger = new Mock<ILogger>();
        logger.Setup(service => service.ErrorAsync(It.IsAny<string>(), It.IsAny<Exception>(), null))
            .Returns(Task.CompletedTask);

        var settings = new StripeBnplSettings
        {
            UseSandbox = true,
            TestWebhookSecret = SigningSecret,
            WebhookSignatureToleranceSeconds = 300
        };
        var pendingCleanup = new Mock<IStripeBnplPendingOrderCleanupService>();
        var service = new StripeBnplWebhookService(
            Mock.Of<IOrderService>(),
            Mock.Of<IOrderProcessingService>(),
            sessionStore.Object,
            pendingCleanup.Object,
            finalizer.Object,
            Mock.Of<IStripeBnplPaymentRecordStore>(),
            eventStore.Object,
            logger.Object,
            settings);

        return new WebhookFixture(service, finalizer, sessionStore, eventStore, pendingCleanup);
    }

    private static string BuildEvent(string eventId, string eventType, string objectId, bool liveMode)
    {
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return JsonSerializer.Serialize(new
        {
            id = eventId,
            @object = "event",
            api_version = global::Stripe.StripeConfiguration.ApiVersion,
            created,
            livemode = liveMode,
            pending_webhooks = 1,
            type = eventType,
            data = new
            {
                @object = new
                {
                    id = objectId,
                    @object = eventType.StartsWith("checkout.session", StringComparison.Ordinal)
                        ? "checkout.session"
                        : "payment_intent",
                    payment_intent = "pi_out_of_order",
                    metadata = new Dictionary<string, string> { ["order_id"] = "42" }
                }
            }
        });
    }

    private static string Sign(string payload)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signedPayload = Encoding.UTF8.GetBytes($"{timestamp}.{payload}");
        var key = Encoding.UTF8.GetBytes(SigningSecret);
        var digest = HMACSHA256.HashData(key, signedPayload);
        return $"t={timestamp},v1={Convert.ToHexString(digest).ToLowerInvariant()}";
    }

    private sealed record WebhookFixture(
        StripeBnplWebhookService Service,
        Mock<IStripeBnplPaymentFinalizer> Finalizer,
        Mock<IStripeBnplCheckoutSessionStore> SessionStore,
        Mock<IStripeBnplWebhookEventStore> EventStore,
        Mock<IStripeBnplPendingOrderCleanupService> PendingCleanup);
}
