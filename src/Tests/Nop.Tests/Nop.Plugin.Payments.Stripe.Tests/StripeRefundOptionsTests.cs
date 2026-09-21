using System.Reflection;
using Nop.Plugin.Payments.Stripe;
using NUnit.Framework;
using Stripe;

namespace Nop.Tests.Nop.Plugin.Payments.Stripe.Tests;

[TestFixture]
public class StripeRefundOptionsTests
{
    [TestCase("pi_test_payment_intent", null)]
    [TestCase("pi_test_payment_intent", 1234L)]
    public void CreateRefundOptions_uses_payment_intent_for_payment_intent_transaction(string transactionId, long? amount)
    {
        var options = CreateOptions(transactionId, amount);

        Assert.That(options.PaymentIntent, Is.EqualTo(transactionId));
        Assert.That(options.Charge, Is.Null);
        Assert.That(options.Amount, Is.EqualTo(amount));
    }

    [Test]
    public void CreateRefundOptions_uses_charge_for_legacy_charge_transaction()
    {
        var options = CreateOptions("ch_test_legacy", null);

        Assert.That(options.Charge, Is.EqualTo("ch_test_legacy"));
        Assert.That(options.PaymentIntent, Is.Null);
    }

    private static RefundCreateOptions CreateOptions(string transactionId, long? amount)
    {
        var method = typeof(StripePaymentProcessor).GetMethod("CreateRefundOptions",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null);
        return (RefundCreateOptions)method.Invoke(null, new object[] { transactionId, amount });
    }
}
