using Microsoft.AspNetCore.Mvc;
using Moq;
using Nop.Core;
using Nop.Core.Domain.Localization;
using Nop.Core.Domain.Stores;
using Nop.Plugin.Misc.HoodLocalizationSeo.Controllers;
using Nop.Services.Catalog;
using Nop.Services.Localization;
using Nop.Services.Seo;
using Nop.Web.Factories;
using NUnit.Framework;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Tests;

[TestFixture]
public sealed class HalloweenLandingControllerTests
{
    [TestCase("zz")]
    [TestCase("gb")]
    [TestCase("EN")]
    public async Task InvalidUnsupportedOrNonCanonicalLanguageRedirectsPermanently(string requestedCode)
    {
        var store = new Store
        {
            Id = 3,
            DefaultLanguageId = 1,
            Url = "https://hoodarcheryshop.com/shop/"
        };
        var storeContext = new Mock<IStoreContext>();
        storeContext.Setup(context => context.GetCurrentStoreAsync()).ReturnsAsync(store);
        var languageService = new Mock<ILanguageService>();
        languageService.Setup(service => service.GetAllLanguagesAsync(false, store.Id))
            .ReturnsAsync(new[]
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
                }
            });
        var categories = new Mock<ICategoryService>(MockBehavior.Strict);
        var webHelper = new Mock<IWebHelper>();
        webHelper.Setup(helper => helper.GetStoreLocation(null)).Returns(store.Url);
        var controller = new HalloweenLandingController(categories.Object,
            languageService.Object, Mock.Of<ILocalizationService>(), Mock.Of<IProductModelFactory>(),
            Mock.Of<IProductService>(), storeContext.Object, Mock.Of<IUrlRecordService>(), webHelper.Object);

        var result = await controller.HalloweenLanding(requestedCode);

        Assert.That(result, Is.TypeOf<RedirectResult>());
        var redirect = (RedirectResult)result;
        Assert.Multiple(() =>
        {
            Assert.That(redirect.Permanent, Is.True);
            Assert.That(redirect.Url, Is.EqualTo(
                "https://hoodarcheryshop.com/shop/en/halloween-archery-and-costume-guide"));
        });
        categories.VerifyNoOtherCalls();
    }

    [Test]
    public async Task StageConfiguredPortUsesTheApprovedPublicOriginForHalloweenRedirects()
    {
        var store = new Store { Id = 3, DefaultLanguageId = 1, Url = "https://hoodarcheryshop.com:47176/" };
        var storeContext = new Mock<IStoreContext>();
        storeContext.Setup(context => context.GetCurrentStoreAsync()).ReturnsAsync(store);
        var languageService = new Mock<ILanguageService>();
        languageService.Setup(service => service.GetAllLanguagesAsync(false, store.Id)).ReturnsAsync(new[]
        {
            new Language { Id = 1, UniqueSeoCode = "en", LanguageCulture = "en-US", Published = true }
        });
        var webHelper = new Mock<IWebHelper>();
        webHelper.Setup(helper => helper.GetStoreLocation(null)).Returns("https://hoodarcheryshop.com/");
        var controller = new HalloweenLandingController(new Mock<ICategoryService>(MockBehavior.Strict).Object,
            languageService.Object, Mock.Of<ILocalizationService>(), Mock.Of<IProductModelFactory>(),
            Mock.Of<IProductService>(), storeContext.Object, Mock.Of<IUrlRecordService>(), webHelper.Object);

        var result = await controller.HalloweenLanding("zz");

        Assert.That(((RedirectResult)result).Url,
            Is.EqualTo("https://hoodarcheryshop.com/en/halloween-archery-and-costume-guide"));
    }
}
