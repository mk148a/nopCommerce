using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Core.Domain.Catalog;
using Nop.Services.Catalog;
using Nop.Services.Localization;
using Nop.Services.Seo;
using Nop.Web.Controllers;

namespace Nop.Plugin.Widgets.CustomProductReviews.Controllers;

/// <summary>
/// Resolves legacy public review URLs to the public product URL that contains
/// the review.  Review text remains canonical product-page content rather than
/// creating a thin, duplicate page per review.
/// </summary>
public sealed class ReviewSeoRedirectController : BasePublicController
{
    private readonly ILanguageService _languageService;
    private readonly IProductService _productService;
    private readonly IUrlRecordService _urlRecordService;

    public ReviewSeoRedirectController(
        ILanguageService languageService,
        IProductService productService,
        IUrlRecordService urlRecordService)
    {
        _languageService = languageService;
        _productService = productService;
        _urlRecordService = urlRecordService;
    }

    /// <summary>
    /// Permanently redirects /{language}/productreviews/{reviewId} to the
    /// localized product page and its review section.
    /// </summary>
    public async Task<IActionResult> ProductReview(int reviewId, string language)
    {
        var review = await _productService.GetProductReviewByIdAsync(reviewId);
        if (review is null || !review.IsApproved)
            return InvokeHttp404();

        var product = await _productService.GetProductByIdAsync(review.ProductId);
        if (product is null || product.Deleted || !product.Published)
            return InvokeHttp404();

        var normalizedLanguage = (language ?? string.Empty).Trim().ToLowerInvariant();
        var targetLanguage = (await _languageService.GetAllLanguagesAsync())
            .FirstOrDefault(item => item.Published &&
                string.Equals(item.UniqueSeoCode, normalizedLanguage, StringComparison.OrdinalIgnoreCase));

        if (targetLanguage is null)
            return InvokeHttp404();

        var slug = await _urlRecordService.GetSeNameAsync(product, targetLanguage.Id);
        if (string.IsNullOrWhiteSpace(slug))
            return InvokeHttp404();

        return LocalRedirectPermanent($"/{targetLanguage.UniqueSeoCode}/{slug}#productreviews");
    }
}
