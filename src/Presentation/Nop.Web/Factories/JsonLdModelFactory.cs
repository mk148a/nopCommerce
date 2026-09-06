using System.Globalization;
using System.Net;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Events;
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
    protected readonly IWebHelper _webHelper;

    #endregion

    #region Ctor

    public JsonLdModelFactory(IEventPublisher eventPublisher,
        IHtmlFormatter htmlFormatter,
        INopUrlHelper nopUrlHelper,
        IProductService productService,
        IWebHelper webHelper)
    {
        _eventPublisher = eventPublisher;
        _htmlFormatter = htmlFormatter;
        _nopUrlHelper = nopUrlHelper;
        _productService = productService;
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
        var imageUrls = model.PictureModels.Select(x => x.FullSizeImageUrl ?? x.ImageUrl)
            .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var description = NormalizePlainText(model.FullDescription) ?? NormalizePlainText(model.ShortDescription);

        var product = new JsonLdProductModel
        {
            Id = $"{productUrl}#product",
            Url = productUrl,
            Name = model.Name,
            Sku = model.Sku,
            Gtin = model.Gtin,
            Mpn = model.ManufacturerPartNumber,
            Description = description,
            Image = imageUrls.Any() ? imageUrls : null,
            Category = model.Breadcrumb?.CategoryBreadcrumb?.LastOrDefault()?.Name,
            Offer = new JsonLdOfferModel
            {
                Id = $"{productUrl}#offer",
                Url = productUrl,
                Price = model.ProductPrice.CallForPrice ? null : model.ProductPrice.PriceValue,
                PriceCurrency = model.ProductPrice.CurrencyCode,
                Availability = $"https://schema.org/{GetAvailability(catalogProduct, model.InStock)}",
                ItemCondition = "https://schema.org/NewCondition",
                Seller = new JsonLdOrganizationModel { Id = $"{_webHelper.GetStoreLocation().TrimEnd('/')}#organization" }
            },
            Brand = model.ProductManufacturers?.Select(manufacturer => new JsonLdBrandModel { Name = manufacturer.Name }).FirstOrDefault()
        };

        // ProductReview has no provenance field, so review schema is deliberately omitted.

        await _eventPublisher.PublishAsync(new JsonLdCreatedEvent<JsonLdProductModel>(product));

        return product;
    }

    protected virtual string NormalizePlainText(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;

        return string.Join(' ', WebUtility.HtmlDecode(_htmlFormatter.StripTags(html))
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

    #endregion
}
