using Microsoft.AspNetCore.Http;
using Moq;
using FluentAssertions;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Localization;
using Nop.Core.Domain.Seo;
using Nop.Core.Domain.Stores;
using Nop.Plugin.Misc.HoodLocalizationSeo.Consumers;
using Nop.Services.Seo;
using Nop.Web.Framework.Events;
using Nop.Web.Framework.Models;
using Nop.Web.Models.Catalog;
using NUnit.Framework;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Tests;

[TestFixture]
public sealed class ProductDescriptionLinkLocalizationConsumerTests
{
    [TestCase(true, "/shop/en/urun-tr?x=1#frag")]
    [TestCase(false, "/shop/urun-tr?x=1#frag")]
    public async Task Rewrites_encoded_product_slug_and_preserves_route_components(bool seoFriendly, string expectedPath)
    {
        var (consumer, model, urlRecords, _) = CreateConsumer(
            seoFriendly, "<a href=\"https://shop.example/shop/%C3%BCrun?x=1#frag\">Ürün</a>");

        await consumer.HandleEventAsync(new ModelPreparedEvent<BaseNopModel>(model));

        urlRecords.Verify(x => x.GetBySlugAsync("ürun"), Times.Once);
        model.FullDescription.Should().Contain($"https://shop.example{expectedPath}");
    }

    [Test]
    public async Task Leaves_external_and_unresolved_links_unchanged()
    {
        var (consumer, model, urlRecords, _) = CreateConsumer(true,
            "<a href=\"https://external.example/old\">external</a>" +
            "<a href=\"https://shop.example/shop/missing\">missing</a>");
        urlRecords.Setup(x => x.GetBySlugAsync("missing")).ReturnsAsync((UrlRecord)null);

        await consumer.HandleEventAsync(new ModelPreparedEvent<BaseNopModel>(model));

        model.FullDescription.Should().Contain("https://external.example/old");
        model.FullDescription.Should().Contain("https://shop.example/shop/missing");
    }

    [Test]
    public async Task Skips_admin_area_requests()
    {
        var (consumer, model, urlRecords, context) = CreateConsumer(true,
            "<a href=\"https://shop.example/shop/%C3%BCrun\">old</a>");
        context.Request.RouteValues["area"] = "Admin";

        await consumer.HandleEventAsync(new ModelPreparedEvent<BaseNopModel>(model));

        model.FullDescription.Should().Contain("https://shop.example/shop/%C3%BCrun");
        urlRecords.Verify(x => x.GetBySlugAsync(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task Processes_post_redisplay_without_area_guard()
    {
        var (consumer, model, _, context) = CreateConsumer(true,
            "<a href=\"https://shop.example/shop/%C3%BCrun\">old</a>");
        context.Request.Method = "POST";

        await consumer.HandleEventAsync(new ModelPreparedEvent<BaseNopModel>(model));

        model.FullDescription.Should().Contain("https://shop.example/shop/en/urun-tr");
    }

    private static (ProductDescriptionLinkLocalizationConsumer Consumer, ProductDetailsModel Model,
        Mock<IUrlRecordService> UrlRecords, DefaultHttpContext Context) CreateConsumer(bool seoFriendly, string description)
    {
        var context = new DefaultHttpContext();
        var contextAccessor = new Mock<IHttpContextAccessor>();
        contextAccessor.SetupGet(x => x.HttpContext).Returns(context);

        var workContext = new Mock<IWorkContext>();
        workContext.Setup(x => x.GetWorkingLanguageAsync()).ReturnsAsync(new Language { Id = 2, UniqueSeoCode = "en" });

        var storeContext = new Mock<IStoreContext>();
        storeContext.Setup(x => x.GetCurrentStoreAsync()).ReturnsAsync(new Store { Url = "https://shop.example/shop/" });

        var urlRecords = new Mock<IUrlRecordService>();
        urlRecords.Setup(x => x.GetBySlugAsync("ürun"))
            .ReturnsAsync(new UrlRecord { EntityId = 42, EntityName = nameof(Product) });
        urlRecords.Setup(x => x.GetSeNameAsync(42, nameof(Product), 2, false, false))
            .ReturnsAsync("urun-tr");

        var settings = new LocalizationSettings { SeoFriendlyUrlsForLanguagesEnabled = seoFriendly };
        return (new ProductDescriptionLinkLocalizationConsumer(contextAccessor.Object, settings,
                storeContext.Object, urlRecords.Object, workContext.Object),
            new ProductDetailsModel { Id = 42, FullDescription = description }, urlRecords, context);
    }
}
