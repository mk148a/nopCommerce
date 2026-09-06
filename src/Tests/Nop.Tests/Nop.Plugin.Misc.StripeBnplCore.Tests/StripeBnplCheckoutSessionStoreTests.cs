using Nop.Plugin.Misc.StripeBnplCore.Services;
using NUnit.Framework;

namespace Nop.Tests.Nop.Plugin.Misc.StripeBnplCore.Tests;

[TestFixture]
public class StripeBnplCheckoutSessionStoreTests
{
    [TestCase("paid", "expired", "paid")]
    [TestCase("paid", "payment_failed", "paid")]
    [TestCase("expired", "open", "expired")]
    [TestCase("payment_failed", "creating", "payment_failed")]
    [TestCase("expired", "paid", "paid")]
    [TestCase("open", "paid", "paid")]
    [TestCase("creating", "open", "open")]
    public void StatusSelectionNeverMovesBackwards(string current, string next, string expected)
    {
        Assert.That(StripeBnplCheckoutSessionStore.SelectMonotonicStatus(current, next), Is.EqualTo(expected));
    }
}
