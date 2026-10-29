using LinqToDB;
using LinqToDB.Data;
using LinqToDB.Mapping;
using Moq;
using Nop.Data;
using Nop.Plugin.Misc.StripeBnplCore.Domain;
using Nop.Plugin.Misc.StripeBnplCore.Services;
using NUnit.Framework;

public class ReservationTests
{
    [TestCase("create_unknown")]
    [TestCase("creating")]
    [TestCase("create_expired")]
    public async Task ActualStoreNeverRotatesUnresolvedExpiredAttempt(string status)
    {
        using var db = new DataConnection(ProviderName.SQLiteMS, "Data Source=:memory:");
        new FluentMappingBuilder(db.MappingSchema).Entity<StripeBnplCheckoutSession>()
            .Property(x => x.Provider).IsNotColumn().Build();
        db.CreateTable<StripeBnplCheckoutSession>();
        var guid = Guid.NewGuid();
        var snapshot = StripeBnplEligibilitySnapshotCodec.Create(new StripeBnplEligibilitySnapshot(
            StripeBnplEligibilitySnapshotCodec.CurrentVersion, 42, guid, BnplProvider.Affirm, "US", "usd", 43910,
            new[] { new StripeBnplEligibilitySnapshotItem(101, 501, 1, "", 701, BnplEligibilityState.Allowed, "Approved", 7, null, "test-v1", DateTime.UtcNow, null) }));
        await db.InsertAsync(new StripeBnplCheckoutSession { Id = 1, OrderId = 42, Provider = BnplProvider.Affirm,
            SessionId = "pending:key-a1", IdempotencyKey = "key-a1", Status = status, AttemptNumber = 1,
            AmountMinor = 43910, Currency = "usd", IsSandbox = true, EligibilitySnapshotHash = snapshot.Sha256,
            EligibilitySnapshotJson = snapshot.Json, ExpiresOnUtc = DateTime.UtcNow.AddDays(-2),
            CreatedOnUtc = DateTime.UtcNow.AddDays(-2), UpdatedOnUtc = DateTime.UtcNow.AddDays(-2) });
        var repo = new Mock<IRepository<StripeBnplCheckoutSession>>();
        repo.SetupGet(x => x.Table).Returns(db.GetTable<StripeBnplCheckoutSession>());
        var store = new StripeBnplCheckoutSessionStore(repo.Object);
        Assert.ThrowsAsync<InvalidOperationException>(async () => await store.ReserveAttemptAsync(42, guid,
            BnplProvider.Affirm, 43910, "usd", true, snapshot));
        Assert.That(await LinqToDB.AsyncExtensions.CountAsync(db.GetTable<StripeBnplCheckoutSession>()), Is.EqualTo(1));
        Assert.That((await store.GetLatestAsync(42, BnplProvider.Affirm)).IdempotencyKey, Is.EqualTo("key-a1"));
        repo.Verify(x => x.InsertAsync(It.IsAny<StripeBnplCheckoutSession>(), false), Times.Never);
    }

    [TestCase("open", "create_rejected", "open")]
    [TestCase("open", "create_unknown", "open")]
    [TestCase("paid", "create_rejected", "paid")]
    [TestCase("paid", "expired", "paid")]
    [TestCase("creating", "create_rejected", "create_rejected")]
    [TestCase("creating", "create_unknown", "create_unknown")]
    public void LateFailureNeverDemotesSuccessfulCreationOrPayment(string current, string next, string expected) =>
        Assert.That(StripeBnplCheckoutSessionStore.SelectMonotonicStatus(current, next), Is.EqualTo(expected));
}
