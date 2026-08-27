using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moq;
using Nop.Core;
using Nop.Core.Domain.Localization;
using Nop.Core.Domain.Seo;
using Nop.Core.Domain.Stores;
using Nop.Core.Domain.Topics;
using Nop.Plugin.Misc.HoodLocalizationSeo.Consumers;
using Nop.Plugin.Misc.HoodLocalizationSeo.Services;
using Nop.Services.Localization;
using Nop.Services.Topics;
using Nop.Web.Framework.Events;
using Nop.Web.Framework.UI;
using NUnit.Framework;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Tests;

[TestFixture]
public sealed class PageRenderingEventConsumerTests
{
    [TestCase("", true, false)]
    [TestCase("?pagenumber=2", false, true)]
    [TestCase("?orderby=5", false, true)]
    public async Task NewProductsUsesCanonicalOnlyForTheQuerylessIndexPage(
        string query,
        bool expectCanonical,
        bool expectNoIndex)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("hoodarcheryshop.com");
        httpContext.Request.Path = "/en/newproducts";
        httpContext.Request.QueryString = new QueryString(query);
        httpContext.Request.RouteValues = new RouteValueDictionary
        {
            ["controller"] = "Catalog",
            ["action"] = "NewProducts",
            ["language"] = "en"
        };
        var headParts = new List<string>();
        var helper = new Mock<INopHtmlHelper>();
        helper.Setup(item => item.GetRouteName(true)).Returns("NewProducts");
        helper.Setup(item => item.AddHeadCustomParts(It.IsAny<string>()))
            .Callback<string>(headParts.Add);
        var consumer = new PageRenderingEventConsumer(
            new HttpContextAccessor { HttpContext = httpContext }, Mock.Of<IBlogTagHreflangService>(),
            Mock.Of<ILocalizationService>(), new SeoSettings { CanonicalUrlsEnabled = true },
            Mock.Of<IStoreContext>(), Mock.Of<ITopicService>(), Mock.Of<IWorkContext>());

        await consumer.HandleEventAsync(new PageRenderingEvent(helper.Object));

        helper.Verify(item => item.AddCanonicalUrlParts(
                "https://hoodarcheryshop.com/en/newproducts", false),
            expectCanonical ? Times.Once() : Times.Never());
        Assert.That(headParts.Contains("<meta name=\"robots\" content=\"noindex,follow\" />"),
            Is.EqualTo(expectNoIndex));
    }

    [TestCase("ProductTagsAll")]
    [TestCase("ManufacturerAll")]
    public async Task UtilityAllPagesAreNoIndexWithoutCanonical(string action)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("hoodarcheryshop.com");
        httpContext.Request.Path = action == "ProductTagsAll"
            ? "/en/producttag/all"
            : "/en/manufacturer/all";
        httpContext.Request.RouteValues = new RouteValueDictionary
        {
            ["controller"] = "Catalog",
            ["action"] = action,
            ["language"] = "en"
        };
        var headParts = new List<string>();
        var helper = new Mock<INopHtmlHelper>();
        helper.Setup(item => item.GetRouteName(true)).Returns(action);
        helper.Setup(item => item.AddHeadCustomParts(It.IsAny<string>()))
            .Callback<string>(headParts.Add);
        var consumer = new PageRenderingEventConsumer(
            new HttpContextAccessor { HttpContext = httpContext }, Mock.Of<IBlogTagHreflangService>(),
            Mock.Of<ILocalizationService>(), new SeoSettings { CanonicalUrlsEnabled = true },
            Mock.Of<IStoreContext>(), Mock.Of<ITopicService>(), Mock.Of<IWorkContext>());

        await consumer.HandleEventAsync(new PageRenderingEvent(helper.Object));

        Assert.That(headParts, Is.EqualTo(new[]
        {
            "<meta name=\"robots\" content=\"noindex,follow\" />"
        }));
        helper.Verify(item => item.AddCanonicalUrlParts(It.IsAny<string>(), It.IsAny<bool>()),
            Times.Never);
    }

    [Test]
    public async Task GenericLocalizedPageDoesNotEmitDuplicateSelfHreflang()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("hoodarcheryshop.com");
        httpContext.Request.Path = "/de/kleidung-mittelalter";
        httpContext.Request.RouteValues = new RouteValueDictionary
        {
            ["controller"] = "Catalog",
            ["action"] = "Category",
            ["language"] = "de"
        };
        var helper = new Mock<INopHtmlHelper>();
        helper.Setup(item => item.GetRouteName(true)).Returns("Category");
        var workContext = new Mock<IWorkContext>(MockBehavior.Strict);
        var consumer = new PageRenderingEventConsumer(
            new HttpContextAccessor { HttpContext = httpContext }, Mock.Of<IBlogTagHreflangService>(),
            Mock.Of<ILocalizationService>(), new SeoSettings(), Mock.Of<IStoreContext>(),
            Mock.Of<ITopicService>(), workContext.Object);

        await consumer.HandleEventAsync(new PageRenderingEvent(helper.Object));

        helper.Verify(item => item.AddHeadCustomParts(It.IsAny<string>()), Times.Never);
        workContext.Verify(context => context.GetWorkingLanguageAsync(), Times.Never);
    }

    [Test]
    public async Task ProductsByTagEmitsNoIndexFollowWithoutTagHreflangWork()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.RouteValues = new RouteValueDictionary
        {
            ["controller"] = "Catalog",
            ["action"] = "ProductsByTag",
            ["productTagId"] = 322
        };
        var headParts = new List<string>();
        var helper = new Mock<INopHtmlHelper>();
        helper.Setup(item => item.GetRouteName(true)).Returns("GenericUrl");
        helper.Setup(item => item.AddHeadCustomParts(It.IsAny<string>()))
            .Callback<string>(headParts.Add);
        var workContext = new Mock<IWorkContext>(MockBehavior.Strict);
        var consumer = new PageRenderingEventConsumer(
            new HttpContextAccessor { HttpContext = httpContext }, Mock.Of<IBlogTagHreflangService>(),
            Mock.Of<ILocalizationService>(), new SeoSettings(),
            Mock.Of<IStoreContext>(), Mock.Of<ITopicService>(), workContext.Object);

        await consumer.HandleEventAsync(new PageRenderingEvent(helper.Object));

        Assert.That(headParts, Is.EqualTo(new[] { "<meta name=\"robots\" content=\"noindex,follow\" />" }));
        workContext.Verify(context => context.GetWorkingLanguageAsync(), Times.Never);
    }

    [Test]
    public async Task BlogByTagStillEmitsExactXDefaultAndTwentyFourLanguageCluster()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("hoodarcheryshop.com");
        httpContext.Request.PathBase = "/shop";
        httpContext.Request.Path = "/de/blog/tag/archery-tag";
        httpContext.Request.QueryString = new QueryString("?page=2");
        httpContext.Request.RouteValues = new RouteValueDictionary
        {
            ["controller"] = "Blog",
            ["action"] = "BlogByTag",
            ["language"] = "de",
            ["tag"] = "archery tag"
        };
        var language = new Language { Id = 9, LanguageCulture = "de-DE" };
        var store = new Store { Id = 1 };
        var targets = Enumerable.Range(0, 24)
            .Select(index => new BlogTagHreflangTarget(
                $"l{index:D2}", $"c{index:D2}", $"etiket {index}", index == 0))
            .ToList();
        var hreflangService = new Mock<IBlogTagHreflangService>();
        hreflangService.Setup(service => service.GetTargetsAsync("archery tag", language, store))
            .ReturnsAsync(targets);
        var workContext = new Mock<IWorkContext>();
        workContext.Setup(context => context.GetWorkingLanguageAsync()).ReturnsAsync(language);
        var storeContext = new Mock<IStoreContext>();
        storeContext.Setup(context => context.GetCurrentStoreAsync()).ReturnsAsync(store);
        var headParts = new List<string>();
        var helper = new Mock<INopHtmlHelper>();
        helper.Setup(item => item.GetRouteName(true)).Returns("BlogByTag");
        helper.Setup(item => item.AddHeadCustomParts(It.IsAny<string>()))
            .Callback<string>(headParts.Add);
        var consumer = new PageRenderingEventConsumer(
            new HttpContextAccessor { HttpContext = httpContext }, hreflangService.Object,
            Mock.Of<ILocalizationService>(), new SeoSettings(), storeContext.Object,
            Mock.Of<ITopicService>(), workContext.Object);

        await consumer.HandleEventAsync(new PageRenderingEvent(helper.Object));

        var expected = new List<string>
        {
            "<meta name=\"robots\" content=\"noindex,follow\" />",
            "<link rel=\"alternate\" hreflang=\"x-default\" href=\"https://hoodarcheryshop.com/shop/l00/blog/tag/etiket%200?page=2\" />"
        };
        expected.AddRange(targets.Select(target =>
            $"<link rel=\"alternate\" hreflang=\"{target.LanguageCulture}\" href=\"https://hoodarcheryshop.com/shop/{target.LanguageCode}/blog/tag/{Uri.EscapeDataString(target.Tag)}?page=2\" />"));
        Assert.That(headParts, Is.EqualTo(expected));
        Assert.That(headParts.Count(part => part.Contains("hreflang=", StringComparison.Ordinal)), Is.EqualTo(25));
        hreflangService.Verify(service => service.GetTargetsAsync("archery tag", language, store), Times.Once);
    }

    [Test]
    public async Task ContactUsUsesCoreViewMetadataHookForLocalizedTopicSeoAndCanonical()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "HTTPS";
        httpContext.Request.Host = new HostString("HOODARCHERYSHOP.COM");
        httpContext.Request.Path = "/de/kontakt";
        httpContext.Request.QueryString = new QueryString("?ref=ad");
        httpContext.Request.RouteValues = new RouteValueDictionary
        {
            ["controller"] = "Common",
            ["action"] = "ContactUs",
            ["language"] = "de"
        };
        var topic = new Topic { Id = 5, SystemName = "ContactUs" };
        var storeContext = new Mock<IStoreContext>();
        storeContext.Setup(context => context.GetCurrentStoreAsync()).ReturnsAsync(new Store { Id = 1 });
        var topics = new Mock<ITopicService>();
        topics.Setup(service => service.GetTopicBySystemNameAsync("ContactUs", 1)).ReturnsAsync(topic);
        var localization = new Mock<ILocalizationService>();
        localization.Setup(service => service.GetLocalizedAsync(topic, entity => entity.MetaDescription,
                null, true, true))
            .ReturnsAsync("Lokalisierte Beschreibung");
        localization.Setup(service => service.GetLocalizedAsync(topic, entity => entity.MetaKeywords,
                null, true, true))
            .ReturnsAsync("bogensport, kontakt");
        var helper = new Mock<INopHtmlHelper>();
        helper.Setup(item => item.GetRouteName(true)).Returns("GenericUrl");
        var workContext = new Mock<IWorkContext>();
        workContext.Setup(context => context.GetWorkingLanguageAsync())
            .ReturnsAsync(new Language { LanguageCulture = "de-DE" });
        var consumer = new PageRenderingEventConsumer(
            new HttpContextAccessor { HttpContext = httpContext }, Mock.Of<IBlogTagHreflangService>(),
            localization.Object, new SeoSettings { CanonicalUrlsEnabled = true }, storeContext.Object,
            topics.Object, workContext.Object);

        await consumer.HandleEventAsync(new PageRenderingEvent(helper.Object));

        helper.Verify(item => item.AddCanonicalUrlParts("https://hoodarcheryshop.com/de/kontakt", false),
            Times.Once);
        helper.Verify(item => item.AddMetaDescriptionParts("Lokalisierte Beschreibung"), Times.Once);
        helper.Verify(item => item.AddMetaKeywordParts("bogensport, kontakt"), Times.Once);
    }

    [Test]
    public async Task HalloweenLandingLeavesHreflangToTheExistingLanguageWidgetWithoutDuplicates()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("hoodarcheryshop.com");
        httpContext.Request.Path = "/de/halloween-archery-and-costume-guide";
        httpContext.Request.RouteValues = new RouteValueDictionary
        {
            ["controller"] = "HalloweenLanding",
            ["action"] = "HalloweenLanding",
            ["language"] = "de"
        };
        var headParts = new List<string>();
        var helper = new Mock<INopHtmlHelper>();
        helper.Setup(item => item.GetRouteName(true)).Returns("HoodHalloweenLanding");
        helper.Setup(item => item.AddHeadCustomParts(It.IsAny<string>())).Callback<string>(headParts.Add);
        var consumer = new PageRenderingEventConsumer(
            new HttpContextAccessor { HttpContext = httpContext }, Mock.Of<IBlogTagHreflangService>(),
            Mock.Of<ILocalizationService>(),
            new SeoSettings { CanonicalUrlsEnabled = true }, Mock.Of<IStoreContext>(),
            Mock.Of<ITopicService>(), Mock.Of<IWorkContext>());

        await consumer.HandleEventAsync(new PageRenderingEvent(helper.Object));

        Assert.That(headParts, Is.Empty);
        helper.Verify(item => item.AddCanonicalUrlParts(
            "https://hoodarcheryshop.com/de/halloween-archery-and-costume-guide", false), Times.Once);
    }
}
