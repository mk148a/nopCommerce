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
using Nop.Core.Domain.Customers;
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
using Nop.Services.Customers;
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
        parsed["aggregateRating"]?["ratingCount"]?.Type.Should().Be(JTokenType.Integer);
        parsed["aggregateRating"]?["reviewCount"].Should().BeNull();
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
    public async Task AllVisibleApprovedRatingsAreIncludedInAggregateAndReviews()
    {
        var stored = new List<ProductReview>
        {
            new() { Id = 1, ProductId = 85, CustomerId = 10, IsApproved = true, Rating = 5, ReviewText = "Great", CreatedOnUtc = DateTime.UtcNow },
            new() { Id = 2, ProductId = 85, CustomerId = 11, IsApproved = true, Rating = 4, ReviewText = "Migrated", CreatedOnUtc = DateTime.UtcNow.AddMinutes(-1) },
            new() { Id = 3, ProductId = 85, CustomerId = 12, IsApproved = true, Rating = 1, ReviewText = "Marketplace", CreatedOnUtc = DateTime.UtcNow.AddMinutes(-2) }
        };
        var visible = stored.Select(review => new ProductReviewModel
        {
            Id = review.Id,
            CustomerId = review.CustomerId,
            CustomerName = $"Customer {review.CustomerId}",
            ReviewText = review.ReviewText,
            Rating = review.Rating
        }).ToList();
        var mappingRepository = CreateRepository<ProductReviewsTransactionsMapping>([
            new() { ProductReviewId = 3, EtsyReviewId = 300 }
        ]);
        var factory = CreateReviewFactory(stored, mappingRepository, []);

        var result = await factory.ReviewSchema(CreateReviewProductModel("arrow2", visible));

        result.AggregateRating.RatingCount.Should().Be(3);
        result.AggregateRating.RatingValue.Should().Be(3.33m);
        result.Reviews.Should().HaveCount(3);
        result.Reviews.Should().Contain(review => review.ReviewBody == "Marketplace");
    }

    [Test]
    public async Task VisibleEtsyTextMatchRemainsAlignedWithAggregate()
    {
        var stored = new List<ProductReview>
        {
            new() { Id = 1, ProductId = 85, CustomerId = 10, IsApproved = true, Rating = 5, ReviewText = "Native review", CreatedOnUtc = DateTime.UtcNow },
            new() { Id = 2, ProductId = 85, CustomerId = 11, IsApproved = true, Rating = 5, ReviewText = "Beautyful in form and flight! As always, excellent arrows!", CreatedOnUtc = DateTime.UtcNow.AddMinutes(-1) }
        };
        var visible = stored.Select(review => new ProductReviewModel
        {
            Id = review.Id,
            CustomerId = review.CustomerId,
            CustomerName = "Real customer",
            ReviewText = review.ReviewText,
            Rating = review.Rating
        }).ToList();
        var factory = CreateReviewFactory(stored, CreateRepository<ProductReviewsTransactionsMapping>([]), [
            new() { Id = 35, Sku = "arrow2", Rating = 5, Review = "Beautyful in form and flight! As always, excellent arrows!" }
        ]);

        var result = await factory.ReviewSchema(CreateReviewProductModel("arrow2", visible));

        result.AggregateRating.RatingCount.Should().Be(2);
        result.Reviews.Should().HaveCount(2);
        result.Reviews.Should().Contain(review => review.ReviewBody == "Native review");
        result.Reviews.Should().Contain(review => review.ReviewBody.Contains("Beautyful"));
    }

    [Test]
    public async Task SyntheticEtsyCustomerMarkerRemainsAlignedWithAggregate()
    {
        var stored = new List<ProductReview>
        {
            new() { Id = 1, ProductId = 85, CustomerId = 901, IsApproved = true, Rating = 5, ReviewText = "Imported marketplace", CreatedOnUtc = DateTime.UtcNow },
            new() { Id = 2, ProductId = 85, CustomerId = 902, IsApproved = true, Rating = 4, ReviewText = "Native review", CreatedOnUtc = DateTime.UtcNow.AddMinutes(-1) }
        };
        var visible = stored.Select(review => new ProductReviewModel
        {
            Id = review.Id,
            CustomerId = review.CustomerId,
            CustomerName = $"Customer {review.CustomerId}",
            ReviewText = review.ReviewText,
            Rating = review.Rating
        }).ToList();
        var customerService = new Mock<ICustomerService>();
        customerService.Setup(service => service.GetCustomerByIdAsync(901))
            .ReturnsAsync(new Customer { Id = 901, Username = "etsy_review_901", Email = "etsy-review-901@hoodarcheryshop.invalid" });
        customerService.Setup(service => service.GetCustomerByIdAsync(902))
            .ReturnsAsync(new Customer { Id = 902, Username = "native-customer", Email = "native@example.test" });
        var factory = CreateReviewFactory(stored, CreateRepository<ProductReviewsTransactionsMapping>([]), [], customerService.Object);

        var result = await factory.ReviewSchema(CreateReviewProductModel("arrow2", visible));

        result.AggregateRating.RatingCount.Should().Be(2);
        result.AggregateRating.RatingValue.Should().Be(4.5m);
        result.Reviews.Should().HaveCount(2);
        result.Reviews.Should().Contain(review => review.ReviewBody == "Native review");
        result.Reviews.Should().Contain(review => review.ReviewBody == "Imported marketplace");
        result.Reviews.Single(review => review.ReviewBody == "Imported marketplace")
            .Author.Name.Should().Be("Anonymous");
        result.Reviews.Should().NotContain(review => review.Author.Name.Contains("etsy_review_", StringComparison.OrdinalIgnoreCase));
    }

    [Test]
    public async Task UnapprovedAndOutOfRangeRatingsAreExcluded()
    {
        var stored = new List<ProductReview>
        {
            new() { Id = 1, ProductId = 85, CustomerId = 10, IsApproved = true, Rating = 5, ReviewText = "Valid", CreatedOnUtc = DateTime.UtcNow },
            new() { Id = 2, ProductId = 85, CustomerId = 11, IsApproved = false, Rating = 5, ReviewText = "Pending", CreatedOnUtc = DateTime.UtcNow },
            new() { Id = 3, ProductId = 85, CustomerId = 12, IsApproved = true, Rating = 0, ReviewText = "Invalid", CreatedOnUtc = DateTime.UtcNow },
            new() { Id = 4, ProductId = 85, CustomerId = 13, IsApproved = true, Rating = 6, ReviewText = "Invalid", CreatedOnUtc = DateTime.UtcNow }
        };
        var visible = stored.Select(review => new ProductReviewModel
        {
            Id = review.Id,
            CustomerId = review.CustomerId,
            CustomerName = "Customer",
            ReviewText = review.ReviewText,
            Rating = review.Rating
        }).ToList();
        var factory = CreateReviewFactory(stored, CreateRepository<ProductReviewsTransactionsMapping>([]), []);

        var result = await factory.ReviewSchema(CreateReviewProductModel("arrow2", visible));

        result.AggregateRating.RatingCount.Should().Be(1);
        result.Reviews.Should().ContainSingle();
    }

    [Test]
    public async Task BlankAuthorCountsTowardAggregateButNotIndividualReview()
    {
        var stored = new List<ProductReview>
        {
            new() { Id = 1, ProductId = 85, CustomerId = 10, IsApproved = true, Rating = 4, ReviewText = "No author", CreatedOnUtc = DateTime.UtcNow },
            new() { Id = 2, ProductId = 85, CustomerId = 11, IsApproved = true, Rating = 5, ReviewText = "Named", CreatedOnUtc = DateTime.UtcNow.AddMinutes(-1) }
        };
        var visible = new List<ProductReviewModel>
        {
            new() { Id = 1, CustomerId = 10, CustomerName = "", ReviewText = "No author", Rating = 4 },
            new() { Id = 2, CustomerId = 11, CustomerName = "Named customer", ReviewText = "Named", Rating = 5 }
        };
        var factory = CreateReviewFactory(stored, CreateRepository<ProductReviewsTransactionsMapping>([]), []);

        var result = await factory.ReviewSchema(CreateReviewProductModel("arrow2", visible));

        result.AggregateRating.RatingCount.Should().Be(2);
        result.Reviews.Should().ContainSingle();
        result.Reviews[0].Author.Name.Should().Be("Named customer");
    }

    [Test]
    public async Task VisibleReviewsRemainCountedAndIndividualReviewsAreLimitedToFive()
    {
        var stored = Enumerable.Range(1, 7).Select(index => new ProductReview
        {
            Id = index,
            ProductId = 85,
            CustomerId = index == 2 ? 1 : index,
            IsApproved = true,
            Rating = 5,
            Title = index is 1 or 2 ? "Same" : $"Title {index}",
            ReviewText = index is 1 or 2 ? "Same body" : $"Body {index}",
            CreatedOnUtc = DateTime.UtcNow.AddMinutes(-index)
        }).ToList();
        var visible = stored.Select(review => new ProductReviewModel
        {
            Id = review.Id,
            CustomerId = review.CustomerId,
            CustomerName = $"Customer {review.Id}",
            ReviewText = review.ReviewText,
            Rating = review.Rating
        }).ToList();
        var factory = CreateReviewFactory(stored, CreateRepository<ProductReviewsTransactionsMapping>([]), []);

        var result = await factory.ReviewSchema(CreateReviewProductModel("arrow2", visible));

        result.AggregateRating.RatingCount.Should().Be(7);
        result.Reviews.Should().HaveCount(5);
        result.Reviews.Should().OnlyContain(review => !string.IsNullOrWhiteSpace(review.ReviewBody));
    }

    [Test]
    public async Task ProductReviewSchemaUsesTheVisibleReviewSet()
    {
        var stored = Enumerable.Range(1, 6).Select(index => new ProductReview
        {
            Id = index,
            ProductId = 85,
            CustomerId = index,
            IsApproved = true,
            Rating = index == 1 ? 4 : 5,
            ReviewText = $"Review {index}",
            CreatedOnUtc = DateTime.UtcNow.AddMinutes(-index)
        }).ToList();
        var firstPageOnly = new ProductReviewModel
        {
            Id = stored[0].Id,
            CustomerId = stored[0].CustomerId,
            CustomerName = "First customer",
            ReviewText = stored[0].ReviewText,
            Rating = stored[0].Rating
        };
        var factory = CreateReviewFactory(stored, CreateRepository<ProductReviewsTransactionsMapping>([]), []);
        var model = CreateReviewProductModel("arrow2", [firstPageOnly]);
        model.Id = 85;

        var result = await factory.ReviewSchema(model);

        result.AggregateRating.RatingCount.Should().Be(1);
        result.AggregateRating.RatingValue.Should().Be(4m);
        result.Reviews.Should().HaveCount(1);
    }

    [Test]
    public async Task ProductWithoutEligibleReviewsOmitsReviewSchema()
    {
        var factory = CreateReviewFactory([], CreateRepository<ProductReviewsTransactionsMapping>([]), []);

        var result = await factory.ReviewSchema(CreateReviewProductModel("arrow2", []));

        result.AggregateRating.Should().BeNull();
        result.Reviews.Should().BeNull();
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

        public TestableJsonLdModelFactory(IRepository<ProductReview> reviewRepository,
            IRepository<ProductReviewsTransactionsMapping> mappingRepository,
            IRepository<EtsyReview> etsyReviewRepository,
            ICustomerService customerService = null)
            : base(Mock.Of<IEventPublisher>(), Mock.Of<IHtmlFormatter>(), Mock.Of<INopUrlHelper>(),
                Mock.Of<IProductService>(), Mock.Of<IRepository<Product>>(), Mock.Of<IWebHelper>(),
                reviewRepository, mappingRepository, etsyReviewRepository, customerService)
        {
        }

        public string Availability(Product product, bool inStock) => GetAvailability(product, inStock);

        public bool IncludeGtin(string gtin, int matchingProductCount) => ShouldIncludeGtin(gtin, matchingProductCount);

        public bool IncludeMpn(string mpn, int matchingProductCount) => ShouldIncludeMpn(mpn, matchingProductCount);

        public Task<(JsonLdAggregateRatingModel AggregateRating, IList<JsonLdReviewModel> Reviews)> ReviewSchema(ProductDetailsModel model) =>
            PrepareReviewSchemaAsync(model);
    }

    private static ProductDetailsModel CreateReviewProductModel(string sku, IList<ProductReviewModel> reviews)
    {
        return new ProductDetailsModel
        {
            Sku = sku,
            ProductReviews = new ProductReviewsModel { Items = reviews }
        };
    }

    private static TestableJsonLdModelFactory CreateReviewFactory(IList<ProductReview> reviews,
        IRepository<ProductReviewsTransactionsMapping> mappingRepository,
        IList<EtsyReview> etsyReviews,
        ICustomerService customerService = null)
    {
        var reviewRepository = CreateRepository(reviews);
        var etsyRepository = CreateRepository(etsyReviews);
        return new TestableJsonLdModelFactory(reviewRepository, mappingRepository, etsyRepository, customerService);
    }

    private static IRepository<TEntity> CreateRepository<TEntity>(IList<TEntity> entities)
        where TEntity : BaseEntity
    {
        var repository = new Mock<IRepository<TEntity>>();
        repository.Setup(item => item.GetAllAsync(It.IsAny<Func<IQueryable<TEntity>, IQueryable<TEntity>>>(),
                null, true))
            .ReturnsAsync(entities);
        return repository.Object;
    }
}
