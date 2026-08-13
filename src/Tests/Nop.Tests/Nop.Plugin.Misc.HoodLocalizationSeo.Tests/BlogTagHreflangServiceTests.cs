using FluentAssertions;
using Moq;
using Nop.Core.Domain.Blogs;
using Nop.Core.Domain.Localization;
using Nop.Core.Domain.Stores;
using Nop.Plugin.Misc.HoodLocalizationSeo.Services;
using Nop.Services.Blogs;
using Nop.Services.Localization;
using NUnit.Framework;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Tests;

[TestFixture]
public sealed class BlogTagHreflangServiceTests
{
    [Test]
    public async Task ReturnsLocalizedTargetsWhenRequestedLabelResolvesToOneSourceTag()
    {
        var translations = new Dictionary<(string Source, int LanguageId), string>
        {
            [("archery", 1)] = "archery",
            [("archery", 2)] = "okçuluk",
            [("quiver", 1)] = "quiver",
            [("quiver", 2)] = "sadak"
        };
        var service = CreateService(translations, "archery", "quiver");
        var store = new Store { Id = 7, DefaultLanguageId = 1 };
        var requestedLanguage = new Language { Id = 2, UniqueSeoCode = "tr", LanguageCulture = "tr-TR" };

        var targets = await service.GetTargetsAsync("okçuluk", requestedLanguage, store);

        targets.Should().BeEquivalentTo(new[]
        {
            new BlogTagHreflangTarget("en", "en-US", "archery", true),
            new BlogTagHreflangTarget("tr", "tr-TR", "okçuluk", false)
        }, options => options.WithStrictOrdering());
    }

    [Test]
    public async Task ReturnsNoTargetsWhenLocalizedLabelMapsToMoreThanOneSourceTag()
    {
        var translations = new Dictionary<(string Source, int LanguageId), string>
        {
            [("shaman costume", 1)] = "shaman costume",
            [("shaman costume", 2)] = "şaman kostümü",
            [("custom shaman costume", 1)] = "custom shaman costume",
            [("custom shaman costume", 2)] = "şaman kostümü"
        };
        var service = CreateService(translations, "shaman costume", "custom shaman costume");
        var store = new Store { Id = 7, DefaultLanguageId = 1 };
        var requestedLanguage = new Language { Id = 2, UniqueSeoCode = "tr", LanguageCulture = "tr-TR" };

        var targets = await service.GetTargetsAsync("şaman kostümü", requestedLanguage, store);

        targets.Should().BeEmpty();
    }

    [Test]
    public async Task ReturnsNoTargetsWhenAProjectedLanguageWouldMapToMoreThanOneSourceTag()
    {
        var translations = new Dictionary<(string Source, int LanguageId), string>
        {
            [("archery", 1)] = "archery",
            [("archery", 2)] = "okçuluk",
            [("traditional archery", 1)] = "traditional archery",
            [("traditional archery", 2)] = "okçuluk"
        };
        var service = CreateService(translations, "archery", "traditional archery");
        var store = new Store { Id = 7, DefaultLanguageId = 1 };
        var requestedLanguage = new Language { Id = 1, UniqueSeoCode = "en", LanguageCulture = "en-US" };

        var targets = await service.GetTargetsAsync("archery", requestedLanguage, store);

        targets.Should().BeEmpty();
    }

    [Test]
    public async Task ReturnsNoTargetsWhenAProjectedLabelEqualsAnotherRawSourceTag()
    {
        var translations = new Dictionary<(string Source, int LanguageId), string>
        {
            [("quiver", 1)] = "quiver",
            [("quiver", 2)] = "tirkeş",
            [("tirkeş", 1)] = "tirkes quiver",
            [("tirkeş", 2)] = "geleneksel tirkeş"
        };
        var service = CreateService(translations, "quiver", "tirkeş");
        var store = new Store { Id = 7, DefaultLanguageId = 1 };
        var requestedLanguage = new Language { Id = 1, UniqueSeoCode = "en", LanguageCulture = "en-US" };

        var targets = await service.GetTargetsAsync("quiver", requestedLanguage, store);

        targets.Should().BeEmpty();
    }

    [Test]
    public async Task ReturnsTheSameReciprocalClusterFromEveryUnambiguousProjection()
    {
        var translations = new Dictionary<(string Source, int LanguageId), string>
        {
            [("archery", 1)] = "archery",
            [("archery", 2)] = "okçuluk",
            [("quiver", 1)] = "quiver",
            [("quiver", 2)] = "sadak"
        };
        var service = CreateService(translations, "archery", "quiver");
        var store = new Store { Id = 7, DefaultLanguageId = 1 };
        var english = new Language { Id = 1, UniqueSeoCode = "en", LanguageCulture = "en-US" };
        var turkish = new Language { Id = 2, UniqueSeoCode = "tr", LanguageCulture = "tr-TR" };

        var englishTargets = await service.GetTargetsAsync("archery", english, store);
        var turkishTargets = await service.GetTargetsAsync("okçuluk", turkish, store);

        englishTargets.Should().Equal(turkishTargets);
        englishTargets.Should().Equal(
            new BlogTagHreflangTarget("en", "en-US", "archery", true),
            new BlogTagHreflangTarget("tr", "tr-TR", "okçuluk", false));
    }

    private static BlogTagHreflangService CreateService(
        IReadOnlyDictionary<(string Source, int LanguageId), string> translations,
        params string[] sourceTags)
    {
        var localization = new Mock<IBlogLocalizationService>();
        localization.Setup(service => service.GetTagLabelAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync((string source, int languageId) => translations[(source, languageId)]);

        var blog = new Mock<IBlogService>();
        blog.Setup(service => service.GetAllBlogPostTagsAsync(7, 1, false))
            .ReturnsAsync(sourceTags.Select(source => new BlogPostTag { Name = source }).ToList());

        var languages = new Mock<ILanguageService>();
        languages.Setup(service => service.GetAllLanguagesAsync(false, 7))
            .ReturnsAsync(new List<Language>
            {
                new()
                {
                    Id = 1, Published = true, DisplayOrder = 1,
                    UniqueSeoCode = "en", LanguageCulture = "en-US"
                },
                new()
                {
                    Id = 2, Published = true, DisplayOrder = 2,
                    UniqueSeoCode = "tr", LanguageCulture = "tr-TR"
                }
            });

        return new BlogTagHreflangService(localization.Object, blog.Object, languages.Object);
    }
}
