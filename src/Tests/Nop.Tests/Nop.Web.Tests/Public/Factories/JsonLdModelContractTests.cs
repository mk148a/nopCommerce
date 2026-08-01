using FluentAssertions;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.AspNetCore.Routing;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nop.Core;
using Nop.Core.Domain;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Media;
using Nop.Core.Domain.Stores;
using Nop.Core.Events;
using Nop.Data;
using Nop.Plugin.Shipping.FixedByWeightByTotal.Services.ProductionTime;
using Nop.Plugin.Shipping.FixedByWeightByTotal.Infrastructure.Seo;
using Nop.Plugin.Shipping.FixedByWeightByTotal;
using Nop.Plugin.Shipping.FixedByWeightByTotal.Components;
using Nop.Services.Catalog;
using Nop.Services.Html;
using Nop.Services.Media;
using Nop.Web.Factories;
using Nop.Web.Framework.Mvc.Routing;
using Nop.Web.Models.JsonLD;
using NUnit.Framework;

namespace Nop.Tests.Nop.Web.Tests.Public.Factories;

[TestFixture]
public class JsonLdModelContractTests
{
    private readonly TestableJsonLdModelFactory _factory = new();

    [Test]
    public void OrderableMadeToOrderProductIsInStock()
    {
        var product = new Product { Published = true, DisableBuyButton = false };

        _factory.Availability(product, inStock: true).Should().Be("InStock");
    }

    [Test]
    public void DisabledProductIsOutOfStock()
    {
        var product = new Product { Published = true, DisableBuyButton = true };

        _factory.Availability(product, inStock: true).Should().Be("OutOfStock");
    }

    [TestCase("9504000059428")]
    [TestCase("9504000059424")]
    [TestCase("not-a-gtin")]
    public void InvalidGtinIsExcluded(string gtin)
    {
        _factory.IncludeGtin(gtin, 1).Should().BeFalse();
    }

    [Test]
    public void DuplicateGtinIsExcludedEvenWhenCheckDigitIsValid()
    {
        _factory.IncludeGtin("4006381333931", 2).Should().BeFalse();
    }

    [Test]
    public void ValidUniqueGtinIsIncluded()
    {
        _factory.IncludeGtin("4006381333931", 1).Should().BeTrue();
    }

    [Test]
    public void ProductJsonUsesNumericPriceAndOmitsEmptyOptionalProperties()
    {
        var model = new JsonLdProductModel
        {
            Name = "Contract test product",
            Offer = new JsonLdOfferModel { Price = 12.50m, PriceCurrency = "USD" }
        };

        var json = JsonConvert.SerializeObject(model, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
        var parsed = JObject.Parse(json);

        parsed["offers"]?["price"]?.Type.Should().Be(JTokenType.Float);
        parsed.Property("hasVariant").Should().BeNull();
        parsed.Property("review").Should().BeNull();
        parsed.Property("aggregateRating").Should().BeNull();
        parsed.Property("priceValidUntil").Should().BeNull();
    }

    [Test]
    public void ProductionAndTransitRangesAreCombinedFromTypedValues()
    {
        var deliveryRange = DeliveryEstimateRange.Combine(21, 28, 2, 5);

        deliveryRange.MinDays.Should().Be(23);
        deliveryRange.MaxDays.Should().Be(33);
    }

    [Test]
    public async Task ConfiguredSystemProductIsSuppressedFromProductSchema()
    {
        var consumer = new SystemProductJsonLdConsumer(new FixedByWeightByTotalSettings
        {
            SystemProductSkus = "expresshipping, internal-payment"
        });
        var model = new JsonLdProductModel { Sku = "expresshipping" };

        await consumer.HandleEventAsync(new JsonLdCreatedEvent<JsonLdProductModel>(model));

        model.SuppressOutput.Should().BeTrue();
        model.SuppressIndex.Should().BeTrue();
    }

    [Test]
    public async Task HomePageOnlineStoreGraphContainsOneWebsiteAndOneOnlineStore()
    {
        var store = new Store
        {
            Url = "https://hoodarcheryshop.com/",
            CompanyName = "Murat KANDİL HOOD ARCHERY SHOP",
            CompanyAddress = "Gürpınar address",
            CompanyPhoneNumber = "+905346406891"
        };
        var storeContext = new Mock<IStoreContext>();
        storeContext.Setup(context => context.GetCurrentStoreAsync()).ReturnsAsync(store);
        var pictureService = new Mock<IPictureService>();
        pictureService.Setup(service => service.GetPictureUrlAsync(3403, It.IsAny<int>(), false,
                It.IsAny<string>(), It.IsAny<PictureType>()))
            .ReturnsAsync("https://hoodarcheryshop.com/images/logo.png");
        var component = new HoodOnlineStoreJsonLdViewComponent(storeContext.Object, new StoreInformationSettings
        {
            LogoPictureId = 3403,
            FacebookLink = "https://www.facebook.com/HoodArchery/",
            YoutubeLink = "https://www.youtube.com/channel/UCch_uO9AyuDygJqqasmS4Rw"
        }, pictureService.Object)
        {
            ViewComponentContext = new ViewComponentContext
            {
                ViewContext = new ViewContext
                {
                    HttpContext = new DefaultHttpContext(),
                    RouteData = new RouteData(new RouteValueDictionary(new { controller = "Home", action = "Index" }))
                }
            }
        };

        var result = await component.InvokeAsync("head_html_tag");
        var html = result.Should().BeOfType<HtmlContentViewComponentResult>().Subject.EncodedContent;
        using var writer = new StringWriter();
        html.WriteTo(writer, System.Text.Encodings.Web.HtmlEncoder.Default);
        var json = JObject.Parse(System.Text.RegularExpressions.Regex.Match(writer.ToString(), ">(?<json>.*)</script>").Groups["json"].Value);
        var graph = (JArray)json["@graph"];

        graph.Should().HaveCount(2);
        graph.Count(node => node["@type"]?.Value<string>() == "WebSite").Should().Be(1);
        graph.Count(node => node["@type"]?.Value<string>() == "OnlineStore").Should().Be(1);
        graph.Single(node => node["@type"]?.Value<string>() == "OnlineStore")["hasMerchantReturnPolicy"]?["merchantReturnLink"]
            ?.Value<string>().Should().Be("https://hoodarcheryshop.com/en/shipping-returns");
    }

    private sealed class TestableJsonLdModelFactory : JsonLdModelFactory
    {
        public TestableJsonLdModelFactory()
            : base(Mock.Of<IEventPublisher>(), Mock.Of<IHtmlFormatter>(), Mock.Of<INopUrlHelper>(),
                Mock.Of<IProductService>(), Mock.Of<IRepository<Product>>(), Mock.Of<IWebHelper>())
        {
        }

        public string Availability(Product product, bool inStock) => GetAvailability(product, inStock);

        public bool IncludeGtin(string gtin, int matchingProductCount) => ShouldIncludeGtin(gtin, matchingProductCount);
    }
}
