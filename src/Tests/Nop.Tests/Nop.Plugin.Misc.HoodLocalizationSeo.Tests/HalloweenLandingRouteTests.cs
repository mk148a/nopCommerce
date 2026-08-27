using Nop.Core.Domain.Localization;
using Nop.Plugin.Misc.HoodLocalizationSeo.Infrastructure;
using NUnit.Framework;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Tests;

[TestFixture]
public sealed class HalloweenLandingRouteTests
{
    [Test]
    public void Language_targets_are_canonical_and_deduplicated()
    {
        var targets = HalloweenLandingRoute.BuildTargets(new Uri("https://hoodarcheryshop.com/shop/"),
            [
                new Language { Id = 2, UniqueSeoCode = "de", LanguageCulture = "de-DE", Published = true },
                new Language { Id = 1, UniqueSeoCode = "en", LanguageCulture = "en-US", Published = true },
                new Language { Id = 3, UniqueSeoCode = "EN", LanguageCulture = "en-GB", Published = true },
                new Language { Id = 4, UniqueSeoCode = "gb", LanguageCulture = "en-GB", Published = true },
                new Language { Id = 5, UniqueSeoCode = "tr", LanguageCulture = "tr-TR", Published = false }
            ], 1);

        Assert.That(targets.Select(target => (target.LanguageCode, target.LanguageCulture,
            target.Url, target.IsDefault)), Is.EqualTo(new[]
        {
            ("en", "en-US", "https://hoodarcheryshop.com/shop/en/halloween-archery-and-costume-guide", true),
            ("de", "de-DE", "https://hoodarcheryshop.com/shop/de/halloween-archery-and-costume-guide", false)
        }));
    }

    [Test]
    public void Path_uses_the_shared_landing_slug()
    {
        Assert.That(HalloweenLandingRoute.BuildPath("tr"),
            Is.EqualTo("/tr/halloween-archery-and-costume-guide"));
    }
}
