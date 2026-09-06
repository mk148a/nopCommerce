using Nop.Data;
using Nop.Plugin.Misc.StripeBnplCore.Domain;
using Nop.Plugin.Misc.StripeBnplCore.Services;
using NUnit.Framework;
using Stripe.Checkout;

namespace Nop.Tests.Nop.Plugin.Misc.StripeBnplCore.Tests;

[TestFixture]
[NonParallelizable]
public class StripeBnplAtomicStoreIntegrationTests : BaseNopTest
{
    [OneTimeSetUp]
    public async Task EnsurePluginSchema()
    {
        // BaseNopTest intentionally installs only nopCommerce's core schema.
        // Create the two plugin tables needed by this isolated SQLite race
        // contract with the same columns and uniqueness constraints as the
        // production FluentMigrator builders.
        var dataProvider = GetService<INopDataProvider>();
        await dataProvider.ExecuteNonQueryAsync("""
            CREATE TABLE IF NOT EXISTS StripeBnplCheckoutSession (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                OrderId INTEGER NOT NULL,
                ProviderId INTEGER NOT NULL,
                SessionId TEXT NOT NULL,
                CheckoutUrl TEXT NULL,
                PaymentIntentId TEXT NULL,
                AmountMinor INTEGER NOT NULL,
                Currency TEXT NOT NULL,
                Status TEXT NOT NULL,
                AttemptNumber INTEGER NOT NULL,
                IdempotencyKey TEXT NOT NULL,
                EligibilitySnapshotJson TEXT NOT NULL,
                EligibilitySnapshotHash TEXT NOT NULL,
                ExpiresOnUtc TEXT NOT NULL,
                IsSandbox INTEGER NOT NULL,
                CreatedOnUtc TEXT NOT NULL,
                UpdatedOnUtc TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS IX_Test_StripeBnplCheckoutSession_SessionId
                ON StripeBnplCheckoutSession(SessionId);
            CREATE UNIQUE INDEX IF NOT EXISTS IX_Test_StripeBnplCheckoutSession_Attempt
                ON StripeBnplCheckoutSession(OrderId, ProviderId, AttemptNumber);
            """);
        await dataProvider.ExecuteNonQueryAsync("""
            CREATE TABLE IF NOT EXISTS StripeBnplWebhookEvent (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                EventId TEXT NOT NULL,
                EventType TEXT NOT NULL,
                ObjectId TEXT NULL,
                OrderId INTEGER NULL,
                ProcessingStatus TEXT NOT NULL,
                ProcessingToken TEXT NULL,
                LeaseExpiresOnUtc TEXT NULL,
                Error TEXT NULL,
                EventCreatedOnUtc TEXT NOT NULL,
                CreatedOnUtc TEXT NOT NULL,
                ProcessedOnUtc TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS IX_Test_StripeBnplWebhookEvent_EventId
                ON StripeBnplWebhookEvent(EventId);
            """);
    }

    [Test]
    public async Task ConcurrentWorkersShareTheUniqueAttemptReservationAndPaidStatusWins()
    {
        var repository = GetService<IRepository<StripeBnplCheckoutSession>>();
        var orderId = CreatePositiveId();
        var orderGuid = Guid.NewGuid();
        var snapshot = CreateSnapshot(orderId, orderGuid);

        try
        {
            var reservations = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ =>
                new StripeBnplCheckoutSessionStore(repository).ReserveAttemptAsync(
                    orderId, orderGuid, BnplProvider.Klarna, 12_550, "usd", true, snapshot)));

            Assert.Multiple(() =>
            {
                Assert.That(reservations.Select(item => item.Id).Distinct().Count(), Is.EqualTo(1));
                Assert.That(reservations.Select(item => item.AttemptNumber).Distinct(), Is.EqualTo(new[] { 1 }));
                Assert.That(reservations[0].EligibilitySnapshotHash, Is.EqualTo(snapshot.Sha256));
            });

            var stripeSession = new Session
            {
                Id = $"cs_atomic_{Guid.NewGuid():N}",
                Url = "https://checkout.stripe.com/c/pay/cs_atomic",
                PaymentIntentId = "pi_atomic",
                Status = "open",
                ExpiresAt = DateTime.UtcNow.AddMinutes(30)
            };
            await Task.WhenAll(reservations.Select(attempt =>
                new StripeBnplCheckoutSessionStore(repository).CompleteAttemptAsync(attempt, stripeSession)));

            var paidWorker = await repository.GetByIdAsync(reservations[0].Id);
            var expiredWorker = await repository.GetByIdAsync(reservations[0].Id);
            await Task.WhenAll(
                new StripeBnplCheckoutSessionStore(repository).UpdateStatusAsync(paidWorker, "paid", "pi_atomic"),
                new StripeBnplCheckoutSessionStore(repository).UpdateStatusAsync(expiredWorker, "expired"));

            var persisted = await repository.GetByIdAsync(reservations[0].Id);
            Assert.Multiple(() =>
            {
                Assert.That(persisted.Status, Is.EqualTo("paid"));
                Assert.That(persisted.PaymentIntentId, Is.EqualTo("pi_atomic"));
            });
        }
        finally
        {
            var rows = await repository.GetAllAsync(query => query.Where(item => item.OrderId == orderId));
            if (rows.Count > 0)
                await repository.DeleteAsync(rows, publishEvent: false);
        }
    }

    [Test]
    public async Task WebhookLeaseHasOneOwnerAndTerminalCompletionIsMonotonic()
    {
        var repository = GetService<IRepository<StripeBnplWebhookEvent>>();
        var eventId = $"evt_atomic_{Guid.NewGuid():N}";
        var envelope = new StripeBnplWebhookEnvelope(
            eventId, "checkout.session.completed", "cs_atomic", 42, DateTime.UtcNow);

        try
        {
            var starts = await Task.WhenAll(Enumerable.Range(0, 4)
                .Select(_ => new StripeBnplWebhookEventStore(repository).TryBeginAsync(envelope)));
            var owner = starts.Single(item => item.State == StripeBnplWebhookBeginState.Started);

            Assert.Multiple(() =>
            {
                Assert.That(owner.ProcessingToken, Has.Length.EqualTo(32));
                Assert.That(starts.Count(item => item.State == StripeBnplWebhookBeginState.InProgress), Is.EqualTo(3));
            });

            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await new StripeBnplWebhookEventStore(repository).CompleteAsync(
                    eventId, Guid.NewGuid().ToString("N"), "ignored", 42));

            await new StripeBnplWebhookEventStore(repository).CompleteAsync(
                eventId, owner.ProcessingToken, "processed", 42);
            var duplicate = await new StripeBnplWebhookEventStore(repository).TryBeginAsync(envelope);
            var persisted = (await repository.GetAllAsync(query => query.Where(item => item.EventId == eventId)))
                .Single();

            Assert.Multiple(() =>
            {
                Assert.That(duplicate.State, Is.EqualTo(StripeBnplWebhookBeginState.Duplicate));
                Assert.That(persisted.ProcessingStatus, Is.EqualTo("processed"));
                Assert.That(persisted.ProcessingToken, Is.Null);
                Assert.That(persisted.LeaseExpiresOnUtc, Is.Null);
            });
        }
        finally
        {
            var rows = await repository.GetAllAsync(query => query.Where(item => item.EventId == eventId));
            if (rows.Count > 0)
                await repository.DeleteAsync(rows, publishEvent: false);
        }
    }

    private static StripeBnplEligibilitySnapshotEnvelope CreateSnapshot(int orderId, Guid orderGuid) =>
        StripeBnplEligibilitySnapshotCodec.Create(new StripeBnplEligibilitySnapshot(
            StripeBnplEligibilitySnapshotCodec.CurrentVersion,
            orderId,
            orderGuid,
            BnplProvider.Klarna,
            "US",
            "usd",
            12_550,
            new[]
            {
                new StripeBnplEligibilitySnapshotItem(101, 501, 1, string.Empty, 701,
                    BnplEligibilityState.Allowed, "Approved", 7, null, "integration-v1",
                    DateTime.UtcNow, null)
            }));

    private static int CreatePositiveId()
    {
        var value = BitConverter.ToInt32(Guid.NewGuid().ToByteArray(), 0) & int.MaxValue;
        return value < 10_000 ? value + 10_000 : value;
    }
}
