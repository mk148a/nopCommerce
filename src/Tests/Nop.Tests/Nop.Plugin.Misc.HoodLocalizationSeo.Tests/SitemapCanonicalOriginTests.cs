using Nop.Plugin.Misc.HoodLocalizationSeo.Infrastructure;
using NUnit.Framework;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Tests;

[TestFixture]
public sealed class SitemapCanonicalOriginTests
{
    [Test]
    public void AllowsTheDefaultPublicPortWhenTheConfiguredStagePortDiffers()
    {
        var valid = SitemapCanonicalOrigin.TryCreate("https://hoodarcheryshop.com:47176/",
            "https://hoodarcheryshop.com/", out var origin);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.True);
            Assert.That(origin.AbsoluteUri, Is.EqualTo("https://hoodarcheryshop.com/"));
        });
    }

    [Test]
    public void RejectsAnActiveStoreLocationWithAnotherHost()
    {
        Assert.That(SitemapCanonicalOrigin.TryCreate("https://hoodarcheryshop.com/",
            "https://untrusted.example/", out _), Is.False);
    }

    [TestCase("https://hoodarcheryshop.com:8443/")]
    [TestCase("https://hoodarcheryshop.com:1337/")]
    public void RejectsAnArbitrarySameHostPort(string activeStoreLocation)
    {
        Assert.That(SitemapCanonicalOrigin.TryCreate("https://hoodarcheryshop.com/",
            activeStoreLocation, out _), Is.False);
    }
}
