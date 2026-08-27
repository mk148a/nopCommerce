using System.Xml.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Media;
using Nop.Data;
using Nop.Plugin.Widgets.HoodVideoSeo.Controllers;
using Nop.Plugin.Widgets.HoodVideoSeo.Services;
using Nop.Services.Catalog;
using Nop.Services.Localization;
using Nop.Services.Media;
using Nop.Services.Seo;
using NUnit.Framework;

namespace Nop.Plugin.Widgets.HoodVideoSeo.Tests;

[TestFixture]
public sealed class VideoSitemapControllerTests
{
    [Test]
    public async Task GetBuildsXmlResponse()
    {
        var languageService = new Mock<ILanguageService>(MockBehavior.Strict);
        languageService
            .Setup(service => service.GetAllLanguagesAsync(false, 0))
            .ReturnsAsync(Array.Empty<Nop.Core.Domain.Localization.Language>());
        var controller = CreateController(languageService);
        controller.Request.Method = HttpMethods.Get;

        var result = await controller.ProductVideos();

        var content = result as ContentResult;
        Assert.Multiple(() =>
        {
            Assert.That(content, Is.Not.Null);
            Assert.That(content!.ContentType, Does.StartWith("application/xml"));
            Assert.That(content.Content, Is.Not.Empty);
            Assert.That(XDocument.Parse(content.Content!).Root?.Name.LocalName, Is.EqualTo("urlset"));
        });
        languageService.Verify(service => service.GetAllLanguagesAsync(false, 0), Times.Once);
    }

    [Test]
    public async Task HeadReturnsXmlSuccessWithoutGeneratingBodyOrLoadingCatalog()
    {
        var languageService = new Mock<ILanguageService>(MockBehavior.Strict);
        var controller = CreateController(languageService);
        controller.Request.Method = HttpMethods.Head;

        var result = await controller.ProductVideos();
        var status = result as StatusCodeResult;

        Assert.Multiple(() =>
        {
            Assert.That(status, Is.Not.Null);
            Assert.That(status!.StatusCode, Is.EqualTo(StatusCodes.Status200OK));
            Assert.That(controller.Response.ContentType, Is.EqualTo("application/xml; charset=utf-8"));
            Assert.That(controller.Response.Body.Length, Is.Zero);
        });
        languageService.VerifyNoOtherCalls();
    }

    private static VideoWatchController CreateController(Mock<ILanguageService> languageService)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        return new VideoWatchController(
            languageService.Object,
            Mock.Of<ILocalizationService>(),
            Mock.Of<IProductService>(),
            Mock.Of<IUrlRecordService>(),
            Mock.Of<IVideoService>(),
            Mock.Of<IRepository<ProductVideo>>(),
            Mock.Of<IRepository<Video>>(),
            Mock.Of<IRepository<Product>>(),
            Mock.Of<IWebHelper>(),
            Mock.Of<IYouTubeVideoAvailabilityService>(),
            Mock.Of<IYouTubePublicationDateService>())
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };
    }
}
