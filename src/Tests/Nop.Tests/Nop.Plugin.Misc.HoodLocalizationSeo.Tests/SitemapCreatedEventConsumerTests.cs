using Moq;
using Nop.Core;
using Nop.Core.Domain.Localization;
using Nop.Core.Domain.Stores;
using Nop.Core.Domain.Topics;
using Nop.Plugin.Misc.HoodLocalizationSeo.Consumers;
using Nop.Plugin.Misc.HoodLocalizationSeo.Services;
using Nop.Services.Blogs;
using Nop.Services.Localization;
using Nop.Services.Seo;
using Nop.Services.Topics;
using Nop.Web.Models.Sitemap;
using NUnit.Framework;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Tests;

[TestFixture]
public sealed class SitemapCreatedEventConsumerTests
{
    private const string StoreLocation = "https://hoodarcheryshop.com";

    [Test]
    public async Task AlignsUnicodeLocationToExactPercentEncodedAlternate()
    {
        const string rawArabic = $"{StoreLocation}/ar/اتصل-بنا?campaign=test";
        const string encodedArabic = $"{StoreLocation}/ar/%D8%A7%D8%AA%D8%B5%D9%84-%D8%A8%D9%86%D8%A7";
        const string german = $"{StoreLocation}/de/kontakt";
        var item = CreateUrl(rawArabic, encodedArabic, german);
        var urls = new List<SitemapUrlModel> { item };

        await CreateConsumer(Array.Empty<Language>())
            .HandleEventAsync(new SitemapCreatedEvent(urls));

        Assert.Multiple(() =>
        {
            Assert.That(urls, Has.Count.EqualTo(1));
            Assert.That(item.Location, Is.EqualTo(encodedArabic));
            Assert.That(item.AlternateLocations, Is.EqualTo(new[] { encodedArabic, german }));
            Assert.That(item.AlternateLocations.Count(location =>
                !location.Equals(item.Location, StringComparison.InvariantCultureIgnoreCase)), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task ReplacesContactModelWhenOnlyAlternateMatchesSemantically()
    {
        var german = new Language { Id = 7, UniqueSeoCode = "de", Published = true };
        var arabic = new Language { Id = 19, UniqueSeoCode = "ar", Published = true };
        const string encodedArabicContact =
            $"{StoreLocation}/ar/%D8%A7%D8%AA%D8%B5%D9%84-%D8%A8%D9%86%D8%A7";
        var oldContact = CreateUrl($"{StoreLocation}/de/not-contact", encodedArabicContact);
        var urls = new List<SitemapUrlModel> { oldContact };

        await CreateConsumer(new[] { german, arabic }, new Topic { Id = 5, SystemName = "ContactUs" },
                new Dictionary<int, string> { [german.Id] = "kontakt", [arabic.Id] = "اتصل-بنا" })
            .HandleEventAsync(new SitemapCreatedEvent(urls));

        Assert.Multiple(() =>
        {
            Assert.That(urls, Has.Count.EqualTo(1));
            Assert.That(urls[0], Is.Not.SameAs(oldContact));
            Assert.That(urls[0].Location, Is.EqualTo($"{StoreLocation}/de/kontakt"));
            Assert.That(urls[0].AlternateLocations, Is.EqualTo(new[]
            {
                $"{StoreLocation}/de/kontakt",
                $"{StoreLocation}/ar/اتصل-بنا"
            }));
        });
    }

    [Test]
    public async Task DeduplicatesUnicodeNormalizationButPreservesReservedPathAndOriginDifferences()
    {
        const string encodedComposed = $"{StoreLocation}/fr/caf%C3%A9";
        var first = CreateUrl($"{StoreLocation}/fr/café?campaign=one", encodedComposed,
            $"{StoreLocation}/de/kaffee");
        var equivalent = CreateUrl($"{StoreLocation}/fr/cafe%CC%81#section");
        var encodedSlash = CreateUrl($"{StoreLocation}/en/a%2Fb");
        var pathSeparator = CreateUrl($"{StoreLocation}/en/a/b");
        var otherOrigin = CreateUrl("https://example.com/fr/caf%C3%A9");
        var urls = new List<SitemapUrlModel>
            { first, equivalent, encodedSlash, pathSeparator, otherOrigin };

        await CreateConsumer(Array.Empty<Language>())
            .HandleEventAsync(new SitemapCreatedEvent(urls));

        Assert.Multiple(() =>
        {
            Assert.That(urls, Has.Count.EqualTo(4));
            Assert.That(urls, Does.Not.Contain(equivalent));
            Assert.That(first.Location, Is.EqualTo(encodedComposed));
            Assert.That(urls.Select(item => item.Location), Does.Contain($"{StoreLocation}/en/a%2Fb"));
            Assert.That(urls.Select(item => item.Location), Does.Contain($"{StoreLocation}/en/a/b"));
            Assert.That(urls.Select(item => item.Location), Does.Contain("https://example.com/fr/caf%C3%A9"));
        });
    }

    private static SitemapCreatedEventConsumer CreateConsumer(IList<Language> languages,
        Topic contactTopic = null, IReadOnlyDictionary<int, string> contactSlugs = null)
    {
        var store = new Store { Id = 3, DefaultLanguageId = 1 };
        var storeContext = new Mock<IStoreContext>();
        storeContext.Setup(context => context.GetCurrentStoreAsync()).ReturnsAsync(store);
        var languageService = new Mock<ILanguageService>();
        languageService.Setup(service => service.GetAllLanguagesAsync(false, store.Id))
            .ReturnsAsync(languages);
        var blogService = new Mock<IBlogService>();
        blogService.Setup(service => service.GetAllBlogPostsAsync(store.Id, store.DefaultLanguageId,
                null, null, 0, int.MaxValue, false, null))
            .ReturnsAsync(new PagedList<Nop.Core.Domain.Blogs.BlogPost>(
                Array.Empty<Nop.Core.Domain.Blogs.BlogPost>(), 0, int.MaxValue));
        var topicService = new Mock<ITopicService>();
        topicService.Setup(service => service.GetTopicBySystemNameAsync("ContactUs", store.Id))
            .ReturnsAsync(contactTopic);
        var urlRecordService = new Mock<IUrlRecordService>();
        if (contactTopic is not null)
        {
            urlRecordService.Setup(service => service.GetSeNameAsync(contactTopic.Id, "Topic",
                    It.IsAny<int?>(), true, false))
                .ReturnsAsync((int _, string _, int? languageId, bool _, bool _) =>
                    contactSlugs?.GetValueOrDefault(languageId ?? 0) ?? string.Empty);
        }
        var webHelper = new Mock<IWebHelper>();
        webHelper.Setup(helper => helper.GetStoreLocation(null)).Returns($"{StoreLocation}/");

        return new SitemapCreatedEventConsumer(Mock.Of<IBlogLocalizationService>(), blogService.Object,
            languageService.Object, storeContext.Object, topicService.Object, urlRecordService.Object,
            webHelper.Object);
    }

    private static SitemapUrlModel CreateUrl(string location, params string[] alternates) =>
        new(location, alternates.ToList(), UpdateFrequency.Weekly,
            new DateTime(2026, 8, 13, 0, 0, 0, DateTimeKind.Utc));
}
