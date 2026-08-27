using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Localization;
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
public sealed class VideoWatchControllerTests
{
    [Test]
    public async Task RandomValidIdWithoutPublishedProductRelationshipNeverQueuesNetworkLookup()
    {
        const int productId = 42;
        const string randomYouTubeId = "AbCdEf12345";
        var languageService = new Mock<ILanguageService>();
        languageService.Setup(service => service.GetAllLanguagesAsync(false, 0))
            .ReturnsAsync(new List<Language> { new() { Id = 1, Published = true, UniqueSeoCode = "en" } });
        var productService = new Mock<IProductService>();
        productService.Setup(service => service.GetProductByIdAsync(productId))
            .ReturnsAsync(new Product
            {
                Id = productId,
                Name = "Published product",
                Published = true,
                VisibleIndividually = true,
                FullDescription = string.Empty,
                ShortDescription = string.Empty
            });
        var videoService = new Mock<IVideoService>();
        videoService.Setup(service => service.GetVideosByProductIdAsync(productId))
            .ReturnsAsync(new List<Video>());
        var availabilityService = new Mock<IYouTubeVideoAvailabilityService>(MockBehavior.Strict);

        var controller = new VideoWatchController(
            languageService.Object,
            Mock.Of<ILocalizationService>(),
            productService.Object,
            Mock.Of<IUrlRecordService>(),
            videoService.Object,
            Mock.Of<IRepository<ProductVideo>>(),
            Mock.Of<IRepository<Video>>(),
            Mock.Of<IRepository<Product>>(),
            Mock.Of<IWebHelper>(),
            availabilityService.Object,
            Mock.Of<IYouTubePublicationDateService>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.Watch("en", productId, randomYouTubeId);

        Assert.That(result, Is.TypeOf<EmptyResult>());
        Assert.That(controller.Response.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
        availabilityService.VerifyNoOtherCalls();
    }

    [TestCase("tPxPcvSjLJk")]
    [TestCase("B7H4igrP0Wo")]
    [TestCase("FxSceVSLx4U")]
    public async Task ReviewedRetiredVideoReturns404WithoutProductOrNetworkLookup(string youtubeId)
    {
        var languageService = new Mock<ILanguageService>(MockBehavior.Strict);
        var productService = new Mock<IProductService>(MockBehavior.Strict);
        var videoService = new Mock<IVideoService>(MockBehavior.Strict);
        var availabilityService = new Mock<IYouTubeVideoAvailabilityService>(MockBehavior.Strict);
        var controller = new VideoWatchController(
            languageService.Object,
            Mock.Of<ILocalizationService>(),
            productService.Object,
            Mock.Of<IUrlRecordService>(),
            videoService.Object,
            Mock.Of<IRepository<ProductVideo>>(),
            Mock.Of<IRepository<Video>>(),
            Mock.Of<IRepository<Product>>(),
            Mock.Of<IWebHelper>(),
            availabilityService.Object,
            Mock.Of<IYouTubePublicationDateService>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.Watch("en", 144, youtubeId);

        Assert.That(result, Is.TypeOf<EmptyResult>());
        Assert.That(controller.Response.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
        languageService.VerifyNoOtherCalls();
        productService.VerifyNoOtherCalls();
        videoService.VerifyNoOtherCalls();
        availabilityService.VerifyNoOtherCalls();
    }
}
