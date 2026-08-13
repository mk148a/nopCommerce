using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moq;
using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Domain.Blogs;
using Nop.Core.Domain.Localization;
using Nop.Core.Domain.Stores;
using Nop.Plugin.Misc.HoodLocalizationSeo.Factories;
using Nop.Plugin.Misc.HoodLocalizationSeo.Services;
using Nop.Services.Blogs;
using Nop.Services.Localization;
using Nop.Web.Factories;
using Nop.Web.Framework.Mvc.Routing;
using Nop.Web.Models.Blogs;
using NUnit.Framework;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Tests;

[TestFixture]
public sealed class LocalizedBlogRoutingTests
{
    [TestCase("de")]
    [TestCase("en")]
    [TestCase(null)]
    public async Task RequestedPathLanguageWinsOverStaleAmbientLanguage(string routeLanguage)
    {
        var english = new Language { Id = 1, UniqueSeoCode = "en", Published = true };
        var german = new Language { Id = 7, UniqueSeoCode = "de", Published = true };
        var context = new DefaultHttpContext();
        context.Request.Path = "/de/blog";
        if (!string.IsNullOrEmpty(routeLanguage))
            context.Request.RouteValues = new RouteValueDictionary
                { [NopRoutingDefaults.RouteValue.Language] = routeLanguage };

        var languageService = new Mock<ILanguageService>();
        languageService.Setup(service => service.GetAllLanguagesAsync(false, 3))
            .ReturnsAsync(new List<Language> { english, german });
        var storeContext = new Mock<IStoreContext>();
        storeContext.Setup(service => service.GetCurrentStoreAsync()).ReturnsAsync(new Store { Id = 3 });
        var workContext = new Mock<IWorkContext>();
        workContext.Setup(service => service.GetWorkingLanguageAsync()).ReturnsAsync(english);

        var resolver = new BlogRouteLanguageResolver(
            new HttpContextAccessor { HttpContext = context },
            languageService.Object,
            storeContext.Object,
            workContext.Object);

        var resolved = await resolver.ResolveAsync();

        Assert.That(resolved.Id, Is.EqualTo(german.Id));
    }

    [Test]
    public async Task BlogModelUsesRequestedRouteLanguageForLocalizedProjection()
    {
        var post = new BlogPost { Id = 12, Title = "English title" };
        var model = new BlogPostModel();
        var german = new Language { Id = 7, UniqueSeoCode = "de", Published = true };
        var inner = new Mock<IBlogModelFactory>();
        inner.Setup(factory => factory.PrepareBlogPostModelAsync(model, post, false))
            .Callback(() => model.SeName = "viking-and-medieval-boots")
            .Returns(Task.CompletedTask);
        var localization = new Mock<IBlogLocalizationService>();
        localization.Setup(service => service.ApplyAsync(model, post, german.Id))
            .Callback(() => model.SeName = "wikinger-und-mittelalterstiefel")
            .Returns(Task.CompletedTask);
        var resolver = new Mock<IBlogRouteLanguageResolver>();
        resolver.Setup(service => service.ResolveAsync()).ReturnsAsync(german);

        var factory = new LocalizedBlogModelFactory(
            inner.Object,
            new BlogSettings(),
            localization.Object,
            Mock.Of<IBlogService>(),
            Mock.Of<IStaticCacheManager>(),
            Mock.Of<IStoreContext>(),
            resolver.Object);

        await factory.PrepareBlogPostModelAsync(model, post, false);

        Assert.That(model.SeName, Is.EqualTo("wikinger-und-mittelalterstiefel"));
        localization.Verify(service => service.ApplyAsync(model, post, german.Id), Times.Once);
    }

    [TestCase("/en/viking-and-medieval-boots", "/de/viking-and-medieval-boots")]
    [TestCase("https://hoodarcheryshop.com/en/viking-and-medieval-boots?x=1",
        "https://hoodarcheryshop.com/de/viking-and-medieval-boots?x=1")]
    public async Task BlogUrlHelperReplacesLeakedEnglishPrefix(string generatedUrl, string expected)
    {
        var inner = new Mock<INopUrlHelper>();
        inner.Setup(helper => helper.RouteGenericUrlAsync<BlogPost>(
                It.IsAny<object>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(generatedUrl);
        var resolver = new Mock<IBlogRouteLanguageResolver>();
        resolver.Setup(service => service.ResolveAsync())
            .ReturnsAsync(new Language { Id = 7, UniqueSeoCode = "de", Published = true });
        var helper = new LocalizedBlogUrlHelper(
            inner.Object,
            resolver.Object,
            new LocalizationSettings { SeoFriendlyUrlsForLanguagesEnabled = true });

        var actual = await helper.RouteGenericUrlAsync<BlogPost>(
            new { SeName = "viking-and-medieval-boots" });

        Assert.That(actual, Is.EqualTo(expected));
    }
}
