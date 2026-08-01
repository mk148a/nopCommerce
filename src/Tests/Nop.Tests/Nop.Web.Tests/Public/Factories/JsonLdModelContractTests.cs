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
using Nop.Core.Domain.Shipping;
using Nop.Core.Domain.Stores;
using Nop.Core.Events;
using Nop.Data;
using Nop.Plugin.Shipping.FixedByWeightByTotal.Services.ProductionTime;
using Nop.Plugin.Shipping.FixedByWeightByTotal.Models.ProductionTime;
using Nop.Plugin.Shipping.FixedByWeightByTotal.Infrastructure.Seo;
using Nop.Plugin.Shipping.FixedByWeightByTotal;
using Nop.Plugin.Shipping.FixedByWeightByTotal.Components;
using Nop.Plugin.Shipping.FixedByWeightByTotal.Infrastructure;
using Nop.Services.Catalog;
using Nop.Services.Html;
using Nop.Services.Media;
using Nop.Web.Factories;
using Nop.Web.Framework.Events;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.Routing;
using Nop.Web.Models.Catalog;
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
    public void MpnIsIncludedOnlyWhenNonBlankAndUnique()
    {
        _factory.IncludeMpn("hood-mpn-001", 1).Should().BeTrue();
        _factory.IncludeMpn("hood-mpn-001", 2).Should().BeFalse();
        _factory.IncludeMpn(" ", 1).Should().BeFalse();
    }

    [Test]
    public void MadeToOrderOrderableProductHidesNumericStockAndUsesPublicAvailabilityText()
    {
        var model = new ProductProductionTimeModel { IsHandmade = true, IsOrderable = true };

        model.HideNumericStock.Should().BeTrue();
        model.AvailabilityText.Should().Be("Available to order — made to order");
    }

    [Test]
    public async Task MadeToOrderPreparedProductModelOmitsNumericStockAndDeliveryDate()
    {
        var service = new Mock<IProductProductionTimeService>();
        service.Setup(x => x.GetModelByProductIdAsync(123)).ReturnsAsync(new ProductProductionTimeModel
        {
            ProductId = 123,
            IsHandmade = true,
            IsOrderable = true
        });
        var consumer = new ProductDetailsModelEventConsumer(service.Object);
        var model = new ProductDetailsModel
        {
            Id = 123,
            StockAvailability = "93 in stock",
            DeliveryDate = "3-5 days"
        };

        await consumer.HandleEventAsync(new ModelPreparedEvent<BaseNopModel>(model));

        model.StockAvailability.Should().BeNull();
        model.DeliveryDate.Should().BeNull();
    }

    [Test]
    public async Task StockedPreparedProductModelKeepsAvailabilityAndDeliveryDate()
    {
        var service = new Mock<IProductProductionTimeService>();
        service.Setup(x => x.GetModelByProductIdAsync(123)).ReturnsAsync(new ProductProductionTimeModel
        {
            ProductId = 123,
            IsHandmade = false,
            IsOrderable = true
        });
        var consumer = new ProductDetailsModelEventConsumer(service.Object);
        var model = new ProductDetailsModel
        {
            Id = 123,
            StockAvailability = "10 in stock",
            DeliveryDate = "3-5 days"
        };

        await consumer.HandleEventAsync(new ModelPreparedEvent<BaseNopModel>(model));

        model.StockAvailability.Should().Be("10 in stock");
        model.DeliveryDate.Should().Be("3-5 days");
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
    public void ProductPayloadUsesSystemTextJsonAndCanonicalOfferUrl()
    {
        var model = new JsonLdProductModel
        {
            Id = "https://hoodarcheryshop.com/en/product#product",
            Url = "https://hoodarcheryshop.com/en/product",
            Name = "Contract test product",
            Offer = new JsonLdOfferModel
            {
                Id = "https://hoodarcheryshop.com/en/product#offer",
                Url = "https://hoodarcheryshop.com/en/product",
                Price = 12.50m,
                PriceCurrency = "USD",
                Availability = "https://schema.org/InStock"
            }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(JsonLdProductPayload.From(model), new System.Text.Json.JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
        var parsed = System.Text.Json.JsonDocument.Parse(json).RootElement;

        parsed.GetProperty("@context").GetString().Should().Be("https://schema.org");
        parsed.GetProperty("@type").GetString().Should().Be("Product");
        parsed.GetProperty("offers").GetProperty("url").GetString().Should().Be(model.Url);
        parsed.GetProperty("offers").GetProperty("price").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Number);
        parsed.TryGetProperty("review", out _).Should().BeFalse();
        parsed.TryGetProperty("hasVariant", out _).Should().BeFalse();
    }

    [Test]
    public void ProductPayloadDropsReviewsWithoutProvenanceSafeFields()
    {
        var model = new JsonLdProductModel
        {
            Review =
            [
                new JsonLdReviewModel
                {
                    Author = new JsonLdPersonModel { Name = "" },
                    DatePublished = "2026-01-01T00:00:00Z",
                    ReviewBody = "Should not be emitted",
                    ReviewRating = new JsonLdRatingModel { RatingValue = 5 }
                },
                new JsonLdReviewModel
                {
                    Author = new JsonLdPersonModel { Name = "Real reviewer" },
                    DatePublished = "2026-01-01T00:00:00Z",
                    ReviewBody = "Valid review",
                    ReviewRating = new JsonLdRatingModel { RatingValue = 5 }
                }
            ]
        };

        var payload = JsonLdProductPayload.From(model);

        payload.Review.Should().ContainSingle();
        payload.Review[0].Author.Name.Should().Be("Real reviewer");
        payload.Review[0].DatePublished.Should().Be("2026-01-01T00:00:00.0000000+00:00");
    }

    [Test]
    public void ProductJsonOmitsReviewFieldsWhenNativeReviewProvenanceIsUnavailable()
    {
        var model = new JsonLdProductModel { Name = "No-provenance review contract" };

        var json = JsonConvert.SerializeObject(model, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
        var parsed = JObject.Parse(json);

        // The current data model cannot prove which reviews are native to this store.
        // Omitting review schema therefore also excludes blank authors and non-ISO dates.
        parsed.Property("review").Should().BeNull();
        parsed.Property("aggregateRating").Should().BeNull();
    }

    [Test]
    public void StructuredRatingFieldsSerializeAsNumbers()
    {
        var model = new JsonLdProductModel
        {
            AggregateRating = new JsonLdAggregateRatingModel
            {
                RatingValue = 4.8955m,
                ReviewCount = 67,
                BestRating = 5m,
                WorstRating = 1m
            },
            Review =
            [
                new JsonLdReviewModel
                {
                    Author = new JsonLdPersonModel { Name = "Real reviewer" },
                    ReviewRating = new JsonLdRatingModel { RatingValue = 5, BestRating = 5m, WorstRating = 1m }
                }
            ]
        };

        var json = JsonConvert.SerializeObject(model, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
        var parsed = JObject.Parse(json);

        parsed["aggregateRating"]?["ratingValue"]?.Type.Should().Be(JTokenType.Float);
        parsed["aggregateRating"]?["bestRating"]?.Type.Should().BeOneOf(JTokenType.Integer, JTokenType.Float);
        parsed["aggregateRating"]?["worstRating"]?.Type.Should().BeOneOf(JTokenType.Integer, JTokenType.Float);
        parsed["review"]?[0]?["reviewRating"]?["bestRating"]?.Type.Should().BeOneOf(JTokenType.Integer, JTokenType.Float);
        parsed["review"]?[0]?["reviewRating"]?["worstRating"]?.Type.Should().BeOneOf(JTokenType.Integer, JTokenType.Float);
    }

    [Test]
    public void VisibleReviewSummaryUsesTheApprovedReviewTotals()
    {
        var overview = new ProductReviewOverviewModel { RatingSum = 328, TotalReviews = 67 };

        overview.AverageRating.Should().Be(4.90m);
    }

    [Test]
    public void EmptyVisibleReviewSummaryHasNoAverage()
    {
        var overview = new ProductReviewOverviewModel();

        overview.AverageRating.Should().Be(0m);
    }

    [Test]
    public void ProductionAndTransitRangesAreCombinedFromTypedValues()
    {
        var deliveryRange = DeliveryEstimateRange.Combine(21, 28, 2, 5);

        deliveryRange.MinDays.Should().Be(23);
        deliveryRange.MaxDays.Should().Be(33);
    }

    [Test]
    public void DestinationSpecificShippingQuoteKeepsHandlingAndTransitRangesSeparate()
    {
        var option = new ShippingOption
        {
            TransitDays = 39,
            TransitMinDays = 3,
            TransitMaxDays = 11,
            HandlingMinDays = 21,
            HandlingMaxDays = 28,
            DestinationCountryCode = "GB"
        };

        option.TransitDays.Should().Be(39);
        option.TransitMinDays.Should().Be(3);
        option.TransitMaxDays.Should().Be(11);
        option.HandlingMinDays.Should().Be(21);
        option.HandlingMaxDays.Should().Be(28);
        option.DestinationCountryCode.Should().Be("GB");

        DeliveryEstimateRange.Combine(option.HandlingMinDays.Value, option.HandlingMaxDays.Value,
            option.TransitMinDays.Value, option.TransitMaxDays.Value).Should().Be((24, 39));
    }

    [Test]
    public void ShippingQuoteWithoutDestinationEstimateDoesNotInventRange()
    {
        var option = new ShippingOption { TransitDays = null };

        option.TransitMinDays.Should().BeNull();
        option.TransitMaxDays.Should().BeNull();
        option.HandlingMinDays.Should().BeNull();
        option.HandlingMaxDays.Should().BeNull();
        option.DestinationCountryCode.Should().BeNull();
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

        public bool IncludeMpn(string mpn, int matchingProductCount) => ShouldIncludeMpn(mpn, matchingProductCount);
    }
}
