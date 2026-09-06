using Moq;
using Nop.Core;
using Nop.Core.Domain.Localization;
using Nop.Core.Domain.Stores;
using Nop.Core.Domain.Topics;
using Nop.Plugin.Misc.HoodLocalizationSeo.Consumers;
using Nop.Plugin.Misc.HoodLocalizationSeo.Services;
using Nop.Services.Blogs;
using Nop.Services.Catalog;
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
    public async Task DoesNotEmitFallbackContactSlugForLanguageWithoutLocalizedUrlRecord()
    {
        var german = new Language { Id = 7, UniqueSeoCode = "de", Published = true };
        var english = new Language { Id = 1, UniqueSeoCode = "en", Published = true };
        var urls = new List<SitemapUrlModel>
        {
            CreateUrl($"{StoreLocation}/de/contactus")
        };

        await CreateConsumer(new[] { german, english }, new Topic { Id = 5, SystemName = "ContactUs" },
                new Dictionary<int, string> { [english.Id] = "contact-us" })
            .HandleEventAsync(new SitemapCreatedEvent(urls));

        var locations = urls.SelectMany(item => new[] { item.Location }
            .Concat(item.AlternateLocations ?? new List<string>())).ToArray();
        Assert.That(locations, Does.Not.Contain($"{StoreLocation}/de/contactus"));
        Assert.That(urls.Single().AlternateLocations,
            Is.EqualTo(new[] { $"{StoreLocation}/en/contact-us" }));
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

    [Test]
    public async Task RemovesDeletedProductButKeepsCustomPrefixWithSameSlug()
    {
        var language = new Language { Id = 1, UniqueSeoCode = "en", Published = true };
        var deleted = new Nop.Core.Domain.Catalog.Product { Id = 42, Deleted = true };
        var deletedUrl = CreateUrl($"{StoreLocation}/en/old-product");
        var customUrl = CreateUrl($"{StoreLocation}/campaign/old-product");
        var foreignUrl = CreateUrl("https://example.com/en/old-product");
        var urls = new List<SitemapUrlModel> { deletedUrl, customUrl, foreignUrl };

        await CreateConsumer(new[] { language }, product: deleted)
            .HandleEventAsync(new SitemapCreatedEvent(urls));

        Assert.That(urls, Has.Count.EqualTo(2));
        Assert.That(urls.Select(url => url.Location), Does.Contain(customUrl.Location));
        Assert.That(urls.Select(url => url.Location), Does.Contain(foreignUrl.Location));
    }

    [Test]
    public async Task AppliesStorePathBaseWhenClassifyingProductRoutes()
    {
        var language = new Language { Id = 1, UniqueSeoCode = "en", Published = true };
        var deleted = new Nop.Core.Domain.Catalog.Product { Id = 42, Deleted = true };
        var removed = CreateUrl($"{StoreLocation}/store/en/old-product");
        var custom = CreateUrl($"{StoreLocation}/store/campaign/old-product");
        var foreign = CreateUrl("https://example.com/store/en/old-product");
        var urls = new List<SitemapUrlModel> { removed, custom, foreign };

        await CreateConsumer(new[] { language }, product: deleted,
                storeUrl: $"{StoreLocation}/store/")
            .HandleEventAsync(new SitemapCreatedEvent(urls));

        Assert.That(urls.Select(url => url.Location), Is.EquivalentTo(new[] { custom.Location, foreign.Location }));
    }

    [Test]
    public async Task KeepsCustomContactUsSuffixRoute()
    {
        var language = new Language { Id = 1, UniqueSeoCode = "en", Published = true };
        var custom = CreateUrl($"{StoreLocation}/campaign/contactus");
        var urls = new List<SitemapUrlModel> { custom };

        await CreateConsumer(new[] { language }, new Topic { Id = 5, SystemName = "ContactUs" },
                new Dictionary<int, string> { [language.Id] = "contactus" })
            .HandleEventAsync(new SitemapCreatedEvent(urls));

        Assert.Multiple(() =>
        {
            Assert.That(urls, Has.Count.EqualTo(2));
            Assert.That(urls.Select(url => url.Location), Does.Contain(custom.Location));
            Assert.That(urls.Select(url => url.Location), Does.Contain($"{StoreLocation}/en/contactus"));
        });
    }

    [Test]
    public async Task RemovesForeignOriginContactSlugRoute()
    {
        var language = new Language { Id = 1, UniqueSeoCode = "en", Published = true };
        var foreign = CreateUrl($"https://example.com/en/contactus");
        var urls = new List<SitemapUrlModel> { foreign };

        await CreateConsumer(new[] { language }, new Topic { Id = 5, SystemName = "ContactUs" },
                new Dictionary<int, string> { [language.Id] = "contactus" })
            .HandleEventAsync(new SitemapCreatedEvent(urls));

        Assert.Multiple(() =>
        {
            Assert.That(urls, Has.Count.EqualTo(1));
            Assert.That(urls.Select(url => url.Location), Does.Not.Contain(foreign.Location));
            Assert.That(urls.SelectMany(url => url.AlternateLocations ?? new List<string>()),
                Does.Not.Contain(foreign.Location));
            Assert.That(urls.Select(url => url.Location), Does.Contain($"{StoreLocation}/en/contactus"));
        });
    }

    [Test]
    public async Task RemovesProtocolRelativeContactRoute()
    {
        var language = new Language { Id = 1, UniqueSeoCode = "en", Published = true };
        var protocolRelative = CreateUrl("//example.com/en/contactus");
        var urls = new List<SitemapUrlModel> { protocolRelative };

        await CreateConsumer(new[] { language }, new Topic { Id = 5, SystemName = "ContactUs" },
                new Dictionary<int, string> { [language.Id] = "contactus" })
            .HandleEventAsync(new SitemapCreatedEvent(urls));

        Assert.Multiple(() =>
        {
            Assert.That(urls, Has.Count.EqualTo(1));
            Assert.That(urls.Select(url => url.Location), Does.Not.Contain(protocolRelative.Location));
            Assert.That(urls.SelectMany(url => url.AlternateLocations ?? new List<string>()),
                Does.Not.Contain(protocolRelative.Location));
            Assert.That(urls.Select(url => url.Location), Does.Contain($"{StoreLocation}/en/contactus"));
        });
    }

    [Test]
    public async Task RemovesForeignLocalizedContactSlugRouteByTopicRecord()
    {
        var german = new Language { Id = 7, UniqueSeoCode = "de", Published = true };
        var foreign = CreateUrl("https://example.com/de/kontakt");
        var urls = new List<SitemapUrlModel> { foreign };

        await CreateConsumer(new[] { german }, new Topic { Id = 5, SystemName = "ContactUs" },
                new Dictionary<int, string> { [german.Id] = "kontakt" })
            .HandleEventAsync(new SitemapCreatedEvent(urls));

        Assert.That(urls.Select(url => url.Location), Does.Not.Contain(foreign.Location));
    }

    [Test]
    public async Task RemovesProtocolRelativeArabicContactSlugRouteByTopicRecord()
    {
        var arabic = new Language { Id = 19, UniqueSeoCode = "ar", Published = true };
        var foreign = CreateUrl("//example.com/ar/اتصل-بنا");
        var urls = new List<SitemapUrlModel> { foreign };

        await CreateConsumer(new[] { arabic }, new Topic { Id = 5, SystemName = "ContactUs" },
                new Dictionary<int, string> { [arabic.Id] = "اتصل-بنا" })
            .HandleEventAsync(new SitemapCreatedEvent(urls));

        Assert.That(urls.Select(url => url.Location), Does.Not.Contain(foreign.Location));
    }

    [Test]
    public async Task PreservesExistingEntriesWhenStoreUrlIsEmpty()
    {
        var existing = CreateUrl($"{StoreLocation}/en/request-derived");
        var urls = new List<SitemapUrlModel> { existing };

        await CreateConsumer(Array.Empty<Language>(), new Topic { Id = 5, SystemName = "ContactUs" },
                storeUrl: string.Empty)
            .HandleEventAsync(new SitemapCreatedEvent(urls));

        Assert.That(urls, Is.EqualTo(new[] { existing }));
    }

    [Test]
    public async Task Uses_https_store_origin_when_ssl_is_enabled()
    {
        var language = new Language { Id = 1, UniqueSeoCode = "en", Published = true };
        var deleted = new Nop.Core.Domain.Catalog.Product { Id = 42, Deleted = true };
        var urls = new List<SitemapUrlModel> { CreateUrl($"{StoreLocation}/en/old-product") };

        await CreateConsumer(new[] { language }, product: deleted, sslEnabled: true)
            .HandleEventAsync(new SitemapCreatedEvent(urls));

        Assert.That(urls, Is.Empty);
    }

    [TestCase("not a url")]
    [TestCase("https://user:secret@hoodarcheryshop.com")]
    [TestCase("https://hoodarcheryshop.com/shop?tenant=one")]
    [TestCase("https://hoodarcheryshop.com/shop#fragment")]
    public async Task Preserves_existing_entries_when_store_url_is_not_a_safe_origin(string storeUrl)
    {
        var urls = new List<SitemapUrlModel>
        {
            CreateUrl($"{StoreLocation}/en/request-derived")
        };

        await CreateConsumer(Array.Empty<Language>(), storeUrl: storeUrl)
            .HandleEventAsync(new SitemapCreatedEvent(urls));

        Assert.That(urls, Is.EqualTo(new[] { urls[0] }));
    }

    [Test]
    public async Task Preserves_custom_store_port_and_path_base_for_generated_contact_urls()
    {
        var language = new Language { Id = 1, UniqueSeoCode = "en", Published = true };
        const string storeUrl = "https://hoodarcheryshop.com:8443/store";
        var urls = new List<SitemapUrlModel>();

        await CreateConsumer(new[] { language }, new Topic { Id = 5, SystemName = "ContactUs" },
                new Dictionary<int, string> { [language.Id] = "contact-us" }, storeUrl: storeUrl)
            .HandleEventAsync(new SitemapCreatedEvent(urls));

        Assert.That(urls.Single().Location, Is.EqualTo($"{storeUrl}/en/contact-us"));
        Assert.That(urls.Single().AlternateLocations, Is.EqualTo(new[] { $"{storeUrl}/en/contact-us" }));
    }

    [Test]
    public async Task EmitsExactlyOneHalloweenClusterForLocalesWithRealTranslatedCopy()
    {
        var languages = new[]
        {
            new Language
            {
                Id = 1, UniqueSeoCode = "en", LanguageCulture = "en-US", Published = true
            },
            new Language
            {
                Id = 2, UniqueSeoCode = "de", LanguageCulture = "de-DE", Published = true
            },
            new Language
            {
                Id = 3, UniqueSeoCode = "gb", LanguageCulture = "en-GB", Published = true
            },
            new Language
            {
                Id = 4, UniqueSeoCode = "tr", LanguageCulture = "tr-TR", Published = false
            }
        };
        var staleUnsupported = CreateUrl(
            $"{StoreLocation}/gb/halloween-archery-and-costume-guide");
        var urls = new List<SitemapUrlModel> { staleUnsupported };

        await CreateConsumer(languages).HandleEventAsync(new SitemapCreatedEvent(urls));

        Assert.That(urls, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(urls[0].Location, Is.EqualTo(
                $"{StoreLocation}/en/halloween-archery-and-costume-guide"));
            Assert.That(urls[0].AlternateLocations, Is.EqualTo(new[]
            {
                $"{StoreLocation}/en/halloween-archery-and-costume-guide",
                $"{StoreLocation}/de/halloween-archery-and-costume-guide"
            }));
            Assert.That(urls, Does.Not.Contain(staleUnsupported));
        });
    }

    private static SitemapCreatedEventConsumer CreateConsumer(IList<Language> languages,
        Topic contactTopic = null, IReadOnlyDictionary<int, string> contactSlugs = null,
        Nop.Core.Domain.Catalog.Product product = null, string storeUrl = StoreLocation,
        bool sslEnabled = false)
    {
        var store = new Store { Id = 3, DefaultLanguageId = 1, SslEnabled = sslEnabled, Url = storeUrl };
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
        var productService = new Mock<IProductService>();
        if (product is not null)
            productService.Setup(service => service.GetProductByIdAsync(product.Id)).ReturnsAsync(product);
        var topicService = new Mock<ITopicService>();
        topicService.Setup(service => service.GetTopicBySystemNameAsync("ContactUs", store.Id))
            .ReturnsAsync(contactTopic);
        var urlRecordService = new Mock<IUrlRecordService>();
        if (product is not null)
            urlRecordService.Setup(service => service.GetBySlugAsync("old-product"))
                .ReturnsAsync(new Nop.Core.Domain.Seo.UrlRecord
                {
                    EntityId = product.Id,
                    EntityName = nameof(Nop.Core.Domain.Catalog.Product)
                });
        if (contactTopic is not null)
        {
                urlRecordService.Setup(service => service.GetSeNameAsync(contactTopic.Id, "Topic",
                    It.IsAny<int?>(), false, false))
                .ReturnsAsync((int _, string _, int? languageId, bool _, bool _) =>
                    contactSlugs?.GetValueOrDefault(languageId ?? 0) ?? string.Empty);
            foreach (var slug in contactSlugs?.Values ?? new List<string>())
                urlRecordService.Setup(service => service.GetBySlugAsync(slug))
                    .ReturnsAsync(new Nop.Core.Domain.Seo.UrlRecord { EntityId = contactTopic.Id, EntityName = "Topic" });
        }
        var webHelper = new Mock<IWebHelper>();
        webHelper.Setup(helper => helper.GetStoreLocation(It.IsAny<bool>())).Returns($"{storeUrl.TrimEnd('/')}/");

        return new SitemapCreatedEventConsumer(Mock.Of<IBlogLocalizationService>(), blogService.Object,
            languageService.Object, productService.Object, storeContext.Object, topicService.Object, urlRecordService.Object,
            webHelper.Object);
    }

    private static SitemapUrlModel CreateUrl(string location, params string[] alternates) =>
        new(location, alternates.ToList(), UpdateFrequency.Weekly,
            new DateTime(2026, 8, 13, 0, 0, 0, DateTimeKind.Utc));
}
