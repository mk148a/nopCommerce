using System.Globalization;
using System.Net;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Events;
using Nop.Data;
using Nop.Services.Catalog;
using Nop.Services.Html;
using Nop.Web.Framework.Mvc.Routing;
using Nop.Web.Models.Catalog;
using Nop.Web.Models.JsonLD;

namespace Nop.Web.Factories;

/// <summary>
/// Represents the JSON-LD model factory implementation
/// </summary>
public partial class JsonLdModelFactory : IJsonLdModelFactory
{
    #region Fields

    protected readonly IEventPublisher _eventPublisher;
    protected readonly IHtmlFormatter _htmlFormatter;
    protected readonly INopUrlHelper _nopUrlHelper;
    protected readonly IProductService _productService;
    protected readonly IRepository<Product> _productRepository;
    protected readonly IRepository<ProductReview> _productReviewRepository;
    protected readonly IRepository<ProductReviewsTransactionsMapping> _productReviewMappingRepository;
    protected readonly IRepository<EtsyReview> _etsyReviewRepository;
    protected readonly IWebHelper _webHelper;

    #endregion

    #region Ctor

    public JsonLdModelFactory(IEventPublisher eventPublisher,
        IHtmlFormatter htmlFormatter,
        INopUrlHelper nopUrlHelper,
        IProductService productService,
        IRepository<Product> productRepository,
        IWebHelper webHelper)
        : this(eventPublisher, htmlFormatter, nopUrlHelper, productService, productRepository, webHelper, null, null, null)
    {
    }

    public JsonLdModelFactory(IEventPublisher eventPublisher,
        IHtmlFormatter htmlFormatter,
        INopUrlHelper nopUrlHelper,
        IProductService productService,
        IRepository<Product> productRepository,
        IWebHelper webHelper,
        IRepository<ProductReview> productReviewRepository,
        IRepository<ProductReviewsTransactionsMapping> productReviewMappingRepository,
        IRepository<EtsyReview> etsyReviewRepository = null)
    {
        _eventPublisher = eventPublisher;
        _htmlFormatter = htmlFormatter;
        _nopUrlHelper = nopUrlHelper;
        _productService = productService;
        _productRepository = productRepository;
        _productReviewRepository = productReviewRepository;
        _productReviewMappingRepository = productReviewMappingRepository;
        _etsyReviewRepository = etsyReviewRepository;
        _webHelper = webHelper;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Prepare JSON-LD category breadcrumb model
    /// </summary>
    /// <param name="categoryModels">List of category models</param>
    /// <returns>A task that represents the asynchronous operation
    /// The task result contains JSON-LD category breadcrumb model
    /// </returns>
    protected virtual async Task<JsonLdBreadcrumbListModel> PrepareJsonLdBreadcrumbListAsync(IList<CategorySimpleModel> categoryModels)
    {
        var breadcrumbList = new JsonLdBreadcrumbListModel();
        var position = 1;

        foreach (var cat in categoryModels)
        {
            var breadcrumbListItem = new JsonLdBreadcrumbListItemModel
            {
                Position = position,
                Item = new JsonLdBreadcrumbItemModel
                {
                    Id = await _nopUrlHelper.RouteGenericUrlAsync<Category>(new { SeName = cat.SeName }, _webHelper.GetCurrentRequestProtocol()),
                    Name = cat.Name
                }
            };
            breadcrumbList.ItemListElement.Add(breadcrumbListItem);
            position++;
        }

        return breadcrumbList;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Prepare JSON-LD category breadcrumb model
    /// </summary>
    /// <param name="categoryModels">List of category models</param>
    /// <returns>A task that represents the asynchronous operation
    /// The task result contains JSON-LD category breadcrumb model
    /// </returns>
    public virtual async Task<JsonLdBreadcrumbListModel> PrepareJsonLdCategoryBreadcrumbAsync(IList<CategorySimpleModel> categoryModels)
    {
        var breadcrumbList = await PrepareJsonLdBreadcrumbListAsync(categoryModels);

        await _eventPublisher.PublishAsync(new JsonLdCreatedEvent<JsonLdBreadcrumbListModel>(breadcrumbList));

        return breadcrumbList;
    }

    /// <summary>
    /// Prepare JSON-LD product breadcrumb model
    /// </summary>
    /// <param name="breadcrumbModel">Product breadcrumb model</param>
    /// <returns>A task that represents the asynchronous operation
    /// The task result contains JSON-LD product breadcrumb model
    /// </returns>
    public virtual async Task<JsonLdBreadcrumbListModel> PrepareJsonLdProductBreadcrumbAsync(ProductDetailsModel.ProductBreadcrumbModel breadcrumbModel)
    {
        var breadcrumbList = await PrepareJsonLdBreadcrumbListAsync(breadcrumbModel.CategoryBreadcrumb);

        breadcrumbList.ItemListElement.Add(new JsonLdBreadcrumbListItemModel
        {
            Position = breadcrumbList.ItemListElement.Count + 1,
            Item = new JsonLdBreadcrumbItemModel
            {
                Id = await _nopUrlHelper.RouteGenericUrlAsync<Product>(new { SeName = breadcrumbModel.ProductSeName }, _webHelper.GetCurrentRequestProtocol()),
                Name = breadcrumbModel.ProductName,
            }
        });

        await _eventPublisher.PublishAsync(new JsonLdCreatedEvent<JsonLdBreadcrumbListModel>(breadcrumbList));

        return breadcrumbList;
    }

    /// <summary>
    /// Prepare JSON-LD product model
    /// </summary>
    /// <param name="model">Product details model</param>
    /// <param name="productUrl">Product URL</param>
    /// <returns>A task that represents the asynchronous operation
    /// The task result contains JSON-LD product model
    /// </returns>
    public virtual async Task<JsonLdProductModel> PrepareJsonLdProductAsync(ProductDetailsModel model, string productUrl = null)
    {
        productUrl ??= await _nopUrlHelper.RouteGenericUrlAsync<Product>(new { SeName = model.SeName }, _webHelper.GetCurrentRequestProtocol());
        productUrl = productUrl.ToLowerInvariant();

        var catalogProduct = await _productService.GetProductByIdAsync(model.Id);
        var matchingGtins = string.IsNullOrWhiteSpace(model.Gtin)
            ? []
            : await _productRepository.GetAllAsync(query => query.Where(product => product.Gtin == model.Gtin && product.Published && !product.Deleted));
        var matchingMpns = string.IsNullOrWhiteSpace(model.ManufacturerPartNumber)
            ? []
            : await _productRepository.GetAllAsync(query => query.Where(product => product.ManufacturerPartNumber == model.ManufacturerPartNumber && product.Published && !product.Deleted));
        var imageUrls = model.PictureModels.Select(x => x.FullSizeImageUrl ?? x.ImageUrl)
            .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var description = NormalizePlainText(model.FullDescription) ?? NormalizePlainText(model.ShortDescription);

        var product = new JsonLdProductModel
        {
            Id = $"{productUrl}#product",
            Url = productUrl,
            Name = model.Name,
            Sku = model.Sku,
            Gtin = ShouldIncludeGtin(model.Gtin, matchingGtins.Count) ? model.Gtin : null,
            Mpn = ShouldIncludeMpn(model.ManufacturerPartNumber, matchingMpns.Count) ? model.ManufacturerPartNumber : null,
            Description = description,
            Image = imageUrls.Any() ? imageUrls : null,
            Category = model.Breadcrumb?.CategoryBreadcrumb?.LastOrDefault()?.Name,
            Offer = model.ProductPrice.CallForPrice ? null : new JsonLdOfferModel
            {
                Id = $"{productUrl}#offer",
                Url = productUrl,
                Price = model.ProductPrice.PriceValue,
                PriceCurrency = model.ProductPrice.CurrencyCode,
                Availability = $"https://schema.org/{GetAvailability(catalogProduct, model.InStock)}",
                ItemCondition = "https://schema.org/NewCondition",
                Seller = new JsonLdOrganizationModel { Id = $"{_webHelper.GetStoreLocation().TrimEnd('/')}#organization" }
            },
            // Keep product and store entity identity aligned; manufacturer catalog data is not used as schema brand data.
            Brand = new JsonLdBrandModel { Name = "Hood Archery Shop" }
        };

        var reviewSchema = await PrepareReviewSchemaAsync(model);
        product.AggregateRating = reviewSchema.AggregateRating;
        product.Review = reviewSchema.Reviews;

        await _eventPublisher.PublishAsync(new JsonLdCreatedEvent<JsonLdProductModel>(product));

        return product;
    }

    /// <summary>
    /// Builds review schema from the same approved ProductReview rows that are
    /// rendered on the product page, while excluding rows explicitly linked to
    /// the legacy Etsy import table.  The mapping table is optional on older
    /// installations; if it cannot be read, schema is omitted rather than
    /// treating unknown provenance as first-party.
    /// </summary>
    protected virtual async Task<(JsonLdAggregateRatingModel AggregateRating, IList<JsonLdReviewModel> Reviews)> PrepareReviewSchemaAsync(ProductDetailsModel model)
    {
        if (_productReviewRepository == null || _productReviewMappingRepository == null)
            return (null, null);

        var visibleItems = model.ProductReviews?.Items ?? [];
        var reviewIds = visibleItems.Select(review => review.Id).Where(id => id > 0).Distinct().ToArray();
        if (reviewIds.Length == 0)
            return (null, null);

        IList<ProductReview> storedReviews;
        IList<ProductReviewsTransactionsMapping> marketplaceMappings;
        try
        {
            storedReviews = await _productReviewRepository.GetAllAsync(query => query
                .Where(review => reviewIds.Contains(review.Id) && review.IsApproved && review.Rating >= 1 && review.Rating <= 5));
            marketplaceMappings = await _productReviewMappingRepository.GetAllAsync(query => query
                .Where(mapping => reviewIds.Contains(mapping.ProductReviewId)));
        }
        catch
        {
            // A missing legacy provenance table is an unknown-provenance state.
            // Do not emit review or aggregateRating markup in that case.
            return (null, null);
        }

        var externalReviewIds = marketplaceMappings
            .Select(mapping => mapping.ProductReviewId)
            .ToHashSet();
        var importedReviewKeys = new HashSet<string>(StringComparer.Ordinal);
        if (_etsyReviewRepository != null && !string.IsNullOrWhiteSpace(model.Sku))
        {
            try
            {
                var importedReviews = await _etsyReviewRepository.GetAllAsync(query => query
                    .Where(review => review.Sku == model.Sku && review.Rating >= 1 && review.Rating <= 5));
                foreach (var importedReview in importedReviews)
                {
                    var normalizedText = NormalizePlainText(importedReview.Review);
                    if (!string.IsNullOrWhiteSpace(normalizedText))
                        importedReviewKeys.Add(BuildExternalReviewKey(importedReview.Rating, normalizedText));
                }
            }
            catch
            {
                // If the legacy table is unavailable, explicit mappings remain
                // authoritative and unknown provenance is not guessed.
            }
        }
        var visibleById = visibleItems.ToDictionary(review => review.Id);
        var seenReviews = new HashSet<string>(StringComparer.Ordinal);
        var eligibleReviews = new List<(ProductReview Stored, ProductReviewModel Visible, string Key)>();

        foreach (var storedReview in storedReviews.OrderByDescending(review => review.CreatedOnUtc).ThenByDescending(review => review.Id))
        {
            if (!storedReview.IsApproved || storedReview.Rating is < 1 or > 5)
                continue;

            var normalizedReviewText = NormalizePlainText(storedReview.ReviewText);
            if (externalReviewIds.Contains(storedReview.Id)
                || importedReviewKeys.Contains(BuildExternalReviewKey(storedReview.Rating, normalizedReviewText))
                || !visibleById.TryGetValue(storedReview.Id, out var visibleReview))
                continue;

            var key = BuildReviewIdentityKey(storedReview);
            if (!seenReviews.Add(key))
                continue;

            eligibleReviews.Add((storedReview, visibleReview, key));
        }

        if (eligibleReviews.Count == 0)
            return (null, null);

        var ratingSum = eligibleReviews.Sum(review => review.Stored.Rating);
        var aggregateRating = new JsonLdAggregateRatingModel
        {
            RatingValue = Math.Round((decimal)ratingSum / eligibleReviews.Count, 2, MidpointRounding.AwayFromZero),
            RatingCount = eligibleReviews.Count,
            BestRating = 5m,
            WorstRating = 1m
        };

        var individualReviews = eligibleReviews
            .Where(review => !string.IsNullOrWhiteSpace(review.Visible.CustomerName)
                && review.Visible.CustomerName.Trim().Length <= 100
                && !string.IsNullOrWhiteSpace(review.Stored.ReviewText))
            .Take(5)
            .Select(review => new JsonLdReviewModel
            {
                Author = new JsonLdPersonModel { Name = review.Visible.CustomerName.Trim() },
                DatePublished = DateTime.SpecifyKind(review.Stored.CreatedOnUtc, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture),
                Name = NormalizePlainText(review.Stored.Title),
                ReviewBody = NormalizePlainText(review.Stored.ReviewText),
                ReviewRating = new JsonLdRatingModel
                {
                    RatingValue = review.Stored.Rating,
                    BestRating = 5m,
                    WorstRating = 1m
                }
            })
            .Where(review => !string.IsNullOrWhiteSpace(review.ReviewBody))
            .ToList();

        return (aggregateRating, individualReviews.Count > 0 ? individualReviews : null);
    }

    protected virtual string BuildReviewIdentityKey(ProductReview review)
    {
        var title = NormalizePlainText(review.Title) ?? string.Empty;
        var body = NormalizePlainText(review.ReviewText) ?? string.Empty;
        return string.Join("|", review.ProductId, review.CustomerId, review.Rating, title, body);
    }

    protected virtual string BuildExternalReviewKey(int rating, string reviewText)
    {
        return $"{rating}|{reviewText ?? string.Empty}";
    }

    protected virtual string NormalizePlainText(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;

        return string.Join(' ', WebUtility.HtmlDecode(_htmlFormatter.StripTags(html) ?? html)
            .Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
    }

    protected virtual string GetAvailability(Product product, bool inStock)
    {
        if (product is null || product.Deleted || !product.Published || product.DisableBuyButton)
            return "OutOfStock";

        if (product.AvailableForPreOrder && product.PreOrderAvailabilityStartDateTimeUtc > DateTime.UtcNow)
            return "PreOrder";

        if (!inStock && product.BackorderMode != BackorderMode.NoBackorders)
            return "BackOrder";

        return inStock ? "InStock" : "OutOfStock";
    }

    protected virtual bool IsValidGtin(string gtin)
    {
        if (string.IsNullOrWhiteSpace(gtin) || gtin.Length is < 8 or > 14 || !gtin.All(char.IsDigit))
            return false;

        var sum = 0;
        for (var index = gtin.Length - 2; index >= 0; index--)
        {
            var positionFromRight = gtin.Length - 2 - index;
            sum += (gtin[index] - '0') * (positionFromRight % 2 == 0 ? 3 : 1);
        }

        return (10 - sum % 10) % 10 == gtin[^1] - '0';
    }

    protected virtual bool ShouldIncludeGtin(string gtin, int matchingProductCount)
    {
        return matchingProductCount == 1 && IsValidGtin(gtin);
    }

    protected virtual bool ShouldIncludeMpn(string mpn, int matchingProductCount)
    {
        return !string.IsNullOrWhiteSpace(mpn) && matchingProductCount == 1;
    }

    #endregion
}
