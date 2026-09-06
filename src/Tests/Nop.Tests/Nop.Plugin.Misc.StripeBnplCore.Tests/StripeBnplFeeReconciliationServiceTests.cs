using Moq;
using Nop.Plugin.Misc.StripeBnplCore.Domain;
using Nop.Plugin.Misc.StripeBnplCore.Services;
using Nop.Services.Logging;
using NUnit.Framework;
using Stripe;

namespace Nop.Tests.Nop.Plugin.Misc.StripeBnplCore.Tests;

[TestFixture]
public class StripeBnplFeeReconciliationServiceTests
{
    [Test]
    public async Task ReconcileCompletesWithActualStripeBalanceTransactionValues()
    {
        var fixture = CreateFixture();
        StripeBnplFeeSnapshot completed = null;
        fixture.Store.Setup(store => store.CompleteFeeReconciliationAsync(fixture.Record.Id,
                It.IsAny<StripeBnplFeeSnapshot>()))
            .Callback<int, StripeBnplFeeSnapshot>((_, snapshot) => completed = snapshot)
            .Returns(Task.CompletedTask);

        var result = await fixture.Service.ReconcileAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(new StripeBnplFeeReconciliationResult(1, 1, 0, 0)));
            Assert.That(completed, Is.Not.Null);
            Assert.That(completed.FeeMinor, Is.EqualTo(850));
            Assert.That(completed.NetMinor, Is.EqualTo(11_700));
            Assert.That(completed.SettlementCurrency, Is.EqualTo("usd"));
            Assert.That(completed.BalanceTransactionId, Is.EqualTo("txn_fee"));
        });
        fixture.Store.Verify(store => store.FailFeeReconciliationAsync(It.IsAny<int>(), It.IsAny<string>()),
            Times.Never);
    }

    [Test]
    public async Task MissingBalanceTransactionBecomesExplicitErrorAndRemainsRetryable()
    {
        var fixture = CreateFixture();
        fixture.PaymentIntent.LatestCharge.BalanceTransaction = null;
        fixture.Store.Setup(store => store.FailFeeReconciliationAsync(fixture.Record.Id, It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var result = await fixture.Service.ReconcileAsync();

        Assert.That(result, Is.EqualTo(new StripeBnplFeeReconciliationResult(1, 0, 1, 0)));
        fixture.Store.Verify(store => store.FailFeeReconciliationAsync(fixture.Record.Id,
            It.Is<string>(message => message.Contains("not available yet"))), Times.Once);
        fixture.Store.Verify(store => store.CompleteFeeReconciliationAsync(
            It.IsAny<int>(), It.IsAny<StripeBnplFeeSnapshot>()), Times.Never);
    }

    [Test]
    public async Task LostClaimSkipsStripeRead()
    {
        var fixture = CreateFixture(StripeBnplFeeReconciliationClaim.InProgress);

        var result = await fixture.Service.ReconcileAsync();

        Assert.That(result, Is.EqualTo(new StripeBnplFeeReconciliationResult(1, 0, 0, 1)));
        fixture.Client.Verify(client => client.GetPaymentIntentAsync(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task ScheduledTaskRunsReconciliationService()
    {
        var service = new Mock<IStripeBnplFeeReconciliationService>();
        service.Setup(item => item.ReconcileAsync(50))
            .ReturnsAsync(new StripeBnplFeeReconciliationResult(0, 0, 0, 0));
        var task = new StripeBnplFeeReconciliationTask(service.Object);

        await task.ExecuteAsync();

        service.Verify(item => item.ReconcileAsync(50), Times.Once);
    }

    private static ReconciliationFixture CreateFixture(
        StripeBnplFeeReconciliationClaim claim = StripeBnplFeeReconciliationClaim.Acquired)
    {
        var record = new StripeBnplPaymentRecord
        {
            Id = 7,
            OrderId = 42,
            Provider = BnplProvider.Klarna,
            PaymentIntentId = "pi_fee",
            ChargeId = "ch_fee",
            AmountMinor = 12_550,
            Currency = "usd",
            Status = "paid",
            FeeDataStatus = StripeBnplFeeDataStatus.Pending,
            IsSandbox = true
        };
        var paymentIntent = new PaymentIntent
        {
            Id = record.PaymentIntentId,
            Amount = record.AmountMinor,
            Currency = record.Currency,
            Status = "succeeded",
            Livemode = false,
            PaymentMethod = new PaymentMethod { Type = "klarna" },
            LatestCharge = new Charge
            {
                Id = record.ChargeId,
                BalanceTransaction = new BalanceTransaction
                {
                    Id = "txn_fee",
                    Fee = 850,
                    Net = 11_700,
                    Currency = "usd",
                    FeeDetails = new List<BalanceTransactionFeeDetail>()
                }
            },
            Metadata = new Dictionary<string, string> { ["order_id"] = record.OrderId.ToString() }
        };

        var store = new Mock<IStripeBnplPaymentRecordStore>();
        store.Setup(item => item.GetFeeReconciliationCandidatesAsync(50, It.IsAny<DateTime>(),
                StripeBnplFeeReconciliationService.MaximumAttempts))
            .ReturnsAsync(new List<StripeBnplPaymentRecord> { record });
        store.Setup(item => item.TryAcquireFeeReconciliationAsync(record.Id, It.IsAny<DateTime>(),
                StripeBnplFeeReconciliationService.MaximumAttempts))
            .ReturnsAsync(claim);

        var client = new Mock<IStripeBnplPaymentClient>();
        client.Setup(item => item.GetPaymentIntentAsync(record.PaymentIntentId)).ReturnsAsync(paymentIntent);
        var guard = new Mock<IStripeBnplEnvironmentGuard>();
        var logger = new Mock<ILogger>();
        logger.Setup(item => item.WarningAsync(It.IsAny<string>(), It.IsAny<Exception>(), null))
            .Returns(Task.CompletedTask);

        return new ReconciliationFixture(
            new StripeBnplFeeReconciliationService(guard.Object, client.Object, store.Object, logger.Object),
            record,
            paymentIntent,
            store,
            client);
    }

    private sealed record ReconciliationFixture(
        StripeBnplFeeReconciliationService Service,
        StripeBnplPaymentRecord Record,
        PaymentIntent PaymentIntent,
        Mock<IStripeBnplPaymentRecordStore> Store,
        Mock<IStripeBnplPaymentClient> Client);
}
