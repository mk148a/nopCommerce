using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Plugin.Widgets.HoodAnswerSeo.Models;
using Nop.Services.Localization;
using Nop.Web.Framework.Components;
using Nop.Web.Framework.Mvc.Routing;
using Nop.Web.Models.Catalog;

namespace Nop.Plugin.Widgets.HoodAnswerSeo.Components;

[ViewComponent(Name = "HoodAnswerSeo")]
public sealed class HoodAnswerSeoViewComponent : NopViewComponent
{
    private const int MaxSummaryLength = 420;
    private readonly INopUrlHelper _nopUrlHelper;
    private readonly IWebHelper _webHelper;
    private readonly ILocalizationService _localizationService;

    public HoodAnswerSeoViewComponent(INopUrlHelper nopUrlHelper, IWebHelper webHelper, ILocalizationService localizationService)
    {
        _nopUrlHelper = nopUrlHelper;
        _webHelper = webHelper;
        _localizationService = localizationService;
    }

    public async Task<IViewComponentResult> InvokeAsync(string widgetZone, object additionalData)
    {
        if (additionalData is not ProductDetailsModel product || product.NoIndex || product.Id <= 0)
            return Content(string.Empty);

        var summary = Truncate(ToPlainText(product.ShortDescription), MaxSummaryLength);
        if (string.IsNullOrWhiteSpace(summary))
            summary = Truncate(ToPlainText(product.FullDescription), MaxSummaryLength);

        var labels = await GetLabelsAsync();
        var category = product.Breadcrumb?.CategoryBreadcrumb?.LastOrDefault()?.Name;
        var sku = product.ShowSku ? product.Sku : null;
        var shipping = product.IsFreeShipping ? labels.FreeShipping : product.DeliveryDate;

        // The sales description and the normal review component already own their content.
        // Render this independent surface only when it has a real, non-duplicated fact to add.
        if (string.IsNullOrWhiteSpace(category) && string.IsNullOrWhiteSpace(sku)
            && string.IsNullOrWhiteSpace(product.StockAvailability) && string.IsNullOrWhiteSpace(shipping)
            && product.ProductReviewOverview.TotalReviews <= 0)
            return Content(string.Empty);

        var canonicalUrl = (await _nopUrlHelper.RouteGenericUrlAsync<Product>(
            new { product.SeName }, _webHelper.GetCurrentRequestProtocol())).ToLowerInvariant();
        var pageSchema = new Dictionary<string, object>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "WebPage",
            ["@id"] = $"{canonicalUrl}#webpage",
            ["url"] = canonicalUrl,
            ["name"] = product.Name,
            ["mainEntity"] = new Dictionary<string, string> { ["@id"] = $"{canonicalUrl}#product" }
        };

        if (!string.IsNullOrWhiteSpace(summary))
            pageSchema["description"] = summary;

        return View("~/Plugins/Widgets.HoodAnswerSeo/Views/Components/HoodAnswerSeo/Default.cshtml",
            new ProductAnswerSurfaceModel(
                category,
                sku,
                product.StockAvailability,
                shipping,
                product.ProductReviewOverview.TotalReviews,
                product.ProductReviewOverview.AverageRating,
                labels,
                JsonSerializer.Serialize(pageSchema)));
    }

    private async Task<AnswerSurfaceLabels> GetLabelsAsync()
    {
        return new AnswerSurfaceLabels(
            await GetLabelAsync("ProductFacts", "Product facts"),
            await GetLabelAsync("WhatItIs", "What it is"),
            await GetLabelAsync("Category", "Category"),
            await GetLabelAsync("Sku", "SKU"),
            await GetLabelAsync("Availability", "Availability"),
            await GetLabelAsync("Shipping", "Shipping"),
            await GetLabelAsync("CustomerRating", "Customer rating"),
            await GetLabelAsync("WhatCustomersSay", "What customers say"),
            await GetLabelAsync("FreeShipping", "Free shipping"),
            await GetLabelAsync("AnonymousCustomer", "Customer"));
    }

    private async Task<string> GetLabelAsync(string resourceName, string fallback)
    {
        var resourceKey = $"Plugins.Widgets.HoodAnswerSeo.{resourceName}";
        var value = await _localizationService.GetResourceAsync(resourceKey);
        return string.IsNullOrWhiteSpace(value) || string.Equals(value, resourceKey, StringComparison.OrdinalIgnoreCase)
            ? fallback
            : value;
    }

    private static string ToPlainText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var noTags = Regex.Replace(value, "<[^>]+>", " ", RegexOptions.CultureInvariant);
        return Regex.Replace(WebUtility.HtmlDecode(noTags), @"\s+", " ", RegexOptions.CultureInvariant).Trim();
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
            return value;

        var sentenceEnd = value.LastIndexOfAny(['.', '!', '?'], maxLength - 1);
        return sentenceEnd >= maxLength / 2 ? value[..(sentenceEnd + 1)] : $"{value[..(maxLength - 1)].TrimEnd()}…";
    }
}
