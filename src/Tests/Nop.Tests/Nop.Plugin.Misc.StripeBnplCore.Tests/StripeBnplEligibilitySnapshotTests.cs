using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Misc.StripeBnplCore.Domain;
using Nop.Plugin.Misc.StripeBnplCore.Services;
using NUnit.Framework;

namespace Nop.Tests.Nop.Plugin.Misc.StripeBnplCore.Tests;

[TestFixture]
public class StripeBnplEligibilitySnapshotTests
{
    [Test]
    public void CapturedAllowedEvidenceRemainsAuditableAfterAdministrativeMatrixChanges()
    {
        var (session, order, orderItems) = CreateValidFixture();

        // The immutable JSON is the evidence used for the already-created
        // attempt; a later matrix edit is deliberately not consulted here.
        var snapshot = StripeBnplEligibilitySnapshotCodec.ValidateForFinalization(session, order, orderItems);

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Items.Single().EligibilityState, Is.EqualTo(BnplEligibilityState.Allowed));
            Assert.That(snapshot.Items.Single().PolicyVersion, Is.EqualTo("policy-2026-08"));
            Assert.That(snapshot.Items.Single().ApprovalReference, Is.EqualTo("CASE-123"));
            Assert.That(snapshot.Items.Single().AttributesXml, Is.EqualTo("<Attributes><Id>7</Id></Attributes>"));
        });
    }

    [Test]
    public void TamperedSnapshotCannotAuthorizePaidTransition()
    {
        var (session, order, orderItems) = CreateValidFixture();
        session.EligibilitySnapshotJson = session.EligibilitySnapshotJson.Replace(
            "policy-2026-08", "policy-tampered", StringComparison.Ordinal);

        var exception = Assert.Throws<NopException>(() =>
            StripeBnplEligibilitySnapshotCodec.ValidateForFinalization(session, order, orderItems));

        Assert.That(exception.Message, Does.Contain("integrity"));
    }

    [Test]
    public void ChangedOrderItemAttributesCannotUseEarlierEligibilityProof()
    {
        var (session, order, orderItems) = CreateValidFixture();
        orderItems[0].AttributesXml = "<Attributes><Id>99</Id></Attributes>";

        var exception = Assert.Throws<NopException>(() =>
            StripeBnplEligibilitySnapshotCodec.ValidateForFinalization(session, order, orderItems));

        Assert.That(exception.Message, Does.Contain("attributes"));
    }

    [TestCase(BnplEligibilityState.Unknown)]
    [TestCase(BnplEligibilityState.ApprovalRequired)]
    [TestCase(BnplEligibilityState.Prohibited)]
    public void NonAllowedEvidenceCannotBePersisted(BnplEligibilityState state)
    {
        var orderGuid = Guid.NewGuid();
        var snapshot = new StripeBnplEligibilitySnapshot(
            StripeBnplEligibilitySnapshotCodec.CurrentVersion,
            42,
            orderGuid,
            BnplProvider.Klarna,
            "US",
            "usd",
            12_550,
            new[]
            {
                new StripeBnplEligibilitySnapshotItem(101, 501, 1, string.Empty, 701, state,
                    "Not allowed", 7, null, "policy-2026-08", DateTime.UtcNow, null)
            });

        var exception = Assert.Throws<NopException>(() => StripeBnplEligibilitySnapshotCodec.Create(snapshot));

        Assert.That(exception.Message, Does.Contain(state.ToString()));
    }

    [TestCase(null)]
    [TestCase(28)]
    public void AccountReviewedAfterpayEvidenceAllowsUnknownOrLongFulfillment(int? fulfillmentDays)
    {
        var snapshot = new StripeBnplEligibilitySnapshot(
            StripeBnplEligibilitySnapshotCodec.CurrentVersion,
            42,
            Guid.NewGuid(),
            BnplProvider.Afterpay,
            "US",
            "usd",
            12_550,
            new[]
            {
                new StripeBnplEligibilitySnapshotItem(101, 501, 1, string.Empty, 701,
                    BnplEligibilityState.Allowed, "Account reviewed", fulfillmentDays, "Stripe Support 2026-08-14",
                    "account-review-2026-08", DateTime.UtcNow, null)
            });

        Assert.DoesNotThrow(() => StripeBnplEligibilitySnapshotCodec.Create(snapshot));
    }

    private static (StripeBnplCheckoutSession Session, Order Order, IList<OrderItem> OrderItems) CreateValidFixture()
    {
        var order = new Order
        {
            Id = 42,
            OrderGuid = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")
        };
        var orderItems = new List<OrderItem>
        {
            new()
            {
                Id = 101,
                OrderId = 42,
                ProductId = 501,
                Quantity = 2,
                AttributesXml = "<Attributes><Id>7</Id></Attributes>"
            }
        };
        var envelope = StripeBnplEligibilitySnapshotCodec.Create(new StripeBnplEligibilitySnapshot(
            StripeBnplEligibilitySnapshotCodec.CurrentVersion,
            order.Id,
            order.OrderGuid,
            BnplProvider.Klarna,
            "US",
            "usd",
            12_550,
            new[]
            {
                new StripeBnplEligibilitySnapshotItem(101, 501, 2, orderItems[0].AttributesXml, 701,
                    BnplEligibilityState.Allowed, "Written exception", 7, "CASE-123", "policy-2026-08",
                    DateTime.UtcNow, "arrow")
            }));
        return (new StripeBnplCheckoutSession
        {
            OrderId = 42,
            Provider = BnplProvider.Klarna,
            AmountMinor = 12_550,
            Currency = "usd",
            EligibilitySnapshotJson = envelope.Json,
            EligibilitySnapshotHash = envelope.Sha256
        }, order, orderItems);
    }
}
