using Moq;
using Nop.Core;
using Nop.Core.Domain.Localization;
using Nop.Core.Domain.Seo;
using Nop.Core.Domain.Stores;
using Nop.Core.Domain.Topics;
using Nop.Plugin.Misc.HoodLocalizationSeo.Factories;
using Nop.Plugin.Misc.HoodLocalizationSeo.Services;
using Nop.Services.Blogs;
using Nop.Services.Localization;
using Nop.Services.Seo;
using Nop.Services.Topics;
using Nop.Web.Factories;
using Nop.Web.Models.Sitemap;
using NUnit.Framework;
using System.Reflection;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Tests;

[TestFixture]
public sealed class LocalizedSitemapModelFactoryTests
{
    [Test]
    public async Task EmptyStoreUrlReturnsInnerModelWithoutRequestOriginRewrite()
    {
        var innerModel = new SitemapModel
        {
            Items =
            {
                new SitemapModel.SitemapItemModel { Name = "Contact", Url = "/contactus" },
                new SitemapModel.SitemapItemModel { Name = "Blog", Url = "/blog/post" }
            }
        };
        var inner = new Mock<ISitemapModelFactory>();
        inner.Setup(factory => factory.PrepareSitemapModelAsync(It.IsAny<SitemapPageModel>()))
            .ReturnsAsync(innerModel);

        var storeContext = new TestStoreContext(new Store { Url = string.Empty });
        var factory = new LocalizedSitemapModelFactory(inner.Object, null, null, storeContext,
            null, null, null, null);

        var result = await factory.PrepareSitemapModelAsync(new SitemapPageModel());

        Assert.That(result, Is.SameAs(innerModel));
        Assert.That(result.Items.Select(item => (item.Name, item.Url)),
            Is.EqualTo(new[] { ("Contact", "/contactus"), ("Blog", "/blog/post") }));
    }

    [Test]
    public void CanonicalStoreOriginPreservesPathBaseAndCustomPort()
    {
        var method = typeof(LocalizedSitemapModelFactory).GetMethod(
            "TryGetCanonicalStoreOrigin", BindingFlags.NonPublic | BindingFlags.Static);
        var args = new object[] { "https://shop.example:8443/store/", null };

        Assert.That((bool)method.Invoke(null, args), Is.True);
        var canonical = (Uri)args[1];
        Assert.That(canonical.ToString(), Is.EqualTo("https://shop.example:8443/store"));

        var build = typeof(LocalizedSitemapModelFactory).GetMethod(
            "BuildLocalizedPath", BindingFlags.NonPublic | BindingFlags.Static);
        var path = (string)build.Invoke(null, new object[] { canonical, "tr", "iletisim" });
        Assert.That(path, Is.EqualTo("/store/tr/iletisim"));
    }

    [Test]
    public async Task MissingLocalizedContactSlugRemovesVerifiedDefaultButKeepsLegacyCandidate()
    {
        var topic = new Topic { Id = 7, SystemName = "ContactUs" };
        var model = new SitemapModel
        {
            Items =
            {
                new SitemapModel.SitemapItemModel { Name = "Default", Url = "/en/contact-us" },
                new SitemapModel.SitemapItemModel { Name = "Legacy", Url = "/en/contactus" }
            }
        };
        var urlRecords = new Mock<IUrlRecordService>();
        urlRecords.Setup(service => service.GetSeNameAsync(topic.Id, "Topic", 1, false, false))
            .ReturnsAsync(string.Empty);
        urlRecords.Setup(service => service.GetSeNameAsync(topic.Id, "Topic", 0, true, false))
            .ReturnsAsync("contact-us");
        urlRecords.Setup(service => service.GetBySlugAsync("contact-us"))
            .ReturnsAsync(new UrlRecord { EntityId = topic.Id, EntityName = "Topic" });
        urlRecords.Setup(service => service.GetBySlugAsync("contactus"))
            .ReturnsAsync((UrlRecord)null);

        var result = await CreateFactory(model, topic, urlRecords).PrepareSitemapModelAsync(new SitemapPageModel());

        Assert.That(result.Items.Select(item => item.Url), Is.EqualTo(new[] { "/en/contactus" }));
    }

    [Test]
    public async Task ExistingLocalizedContactSlugRewritesLegacyAndDeduplicates()
    {
        var topic = new Topic { Id = 7, SystemName = "ContactUs" };
        var model = new SitemapModel
        {
            Items =
            {
                new SitemapModel.SitemapItemModel { Name = "Legacy", Url = "/en/contactus" },
                new SitemapModel.SitemapItemModel { Name = "Localized", Url = "/en/kontakt" }
            }
        };
        var urlRecords = new Mock<IUrlRecordService>();
        urlRecords.Setup(service => service.GetSeNameAsync(topic.Id, "Topic", 1, false, false))
            .ReturnsAsync("kontakt");
        urlRecords.Setup(service => service.GetBySlugAsync("kontakt"))
            .ReturnsAsync(new UrlRecord { EntityId = topic.Id, EntityName = "Topic" });
        urlRecords.Setup(service => service.GetBySlugAsync("contactus"))
            .ReturnsAsync((UrlRecord)null);

        var result = await CreateFactory(model, topic, urlRecords).PrepareSitemapModelAsync(new SitemapPageModel());

        Assert.That(result.Items.Select(item => item.Url), Is.EqualTo(new[] { "/en/kontakt" }));
    }

    private static LocalizedSitemapModelFactory CreateFactory(SitemapModel model, Topic topic,
        Mock<IUrlRecordService> urlRecords)
    {
        var store = new Store { Id = 1, DefaultLanguageId = 1, Url = "https://example.test" };
        var language = new Language { Id = 1, UniqueSeoCode = "en", Published = true };
        var inner = new Mock<ISitemapModelFactory>();
        inner.Setup(factory => factory.PrepareSitemapModelAsync(It.IsAny<SitemapPageModel>()))
            .ReturnsAsync(model);
        var blogService = new Mock<IBlogService>();
        blogService.Setup(service => service.GetAllBlogPostsAsync(store.Id, store.DefaultLanguageId,
                null, null, 0, int.MaxValue, false, null))
            .ReturnsAsync(new PagedList<Nop.Core.Domain.Blogs.BlogPost>(Array.Empty<Nop.Core.Domain.Blogs.BlogPost>(), 0, int.MaxValue));
        var topicService = new Mock<ITopicService>();
        topicService.Setup(service => service.GetTopicBySystemNameAsync("ContactUs", store.Id)).ReturnsAsync(topic);
        var languageService = new Mock<ILanguageService>();
        languageService.Setup(service => service.GetAllLanguagesAsync(false, store.Id)).ReturnsAsync(new[] { language });
        var workContext = new Mock<IWorkContext>();
        workContext.Setup(service => service.GetWorkingLanguageAsync()).ReturnsAsync(language);
        return new LocalizedSitemapModelFactory(inner.Object, Mock.Of<IBlogLocalizationService>(), blogService.Object,
            new TestStoreContext(store), topicService.Object, urlRecords.Object, workContext.Object, languageService.Object);
    }

    private sealed class TestStoreContext(Store store) : IStoreContext
    {
        public Task<Store> GetCurrentStoreAsync() => Task.FromResult(store);
        public Store GetCurrentStore() => store;
        public Task<int> GetActiveStoreScopeConfigurationAsync() => Task.FromResult(store.Id);
    }
}
