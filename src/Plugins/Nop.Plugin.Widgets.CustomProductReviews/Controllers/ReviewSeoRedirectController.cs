using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Core.Domain.Catalog;
using Nop.Data;
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
    private readonly IRepository<ProductReview> _productReviewRepository;
    private readonly IRepository<EtsyReview> _etsyReviewRepository;
    private readonly IUrlRecordService _urlRecordService;

    public ReviewSeoRedirectController(
        ILanguageService languageService,
        IProductService productService,
        IRepository<ProductReview> productReviewRepository,
        IRepository<EtsyReview> etsyReviewRepository,
        IUrlRecordService urlRecordService)
    {
        _languageService = languageService;
        _productService = productService;
        _productReviewRepository = productReviewRepository;
        _etsyReviewRepository = etsyReviewRepository;
        _urlRecordService = urlRecordService;
    }

    /// <summary>
    /// Permanently redirects /{language}/productreviews/{reviewId} to the
    /// localized product page and its review section. Older Hood URLs used
    /// the product ID here, while newer URLs used the review ID; support both
    /// forms without generating a standalone, thin review page.
    /// </summary>
    public async Task<IActionResult> ProductReview(int reviewId, string language)
    {
        var review = await _productService.GetProductReviewByIdAsync(reviewId);
        Product product = null;
        var includeReviewFragment = false;

        if (review is not null)
        {
            if (!review.IsApproved)
                return InvokeHttp404();

            product = await _productService.GetProductByIdAsync(review.ProductId);
            includeReviewFragment = true;
        }
        else
        {
            // The pre-plugin route encoded Product.Id.  Do not redirect a
            // made-up review URL: require at least one visible review first.
            var productByLegacyId = await _productService.GetProductByIdAsync(reviewId);
            var approvedReviews = productByLegacyId is null
                ? []
                : await _productReviewRepository.GetAllAsync(query => query.Where(item =>
                    item.ProductId == productByLegacyId.Id && item.IsApproved));

            if (productByLegacyId is not null && approvedReviews.Count > 0)
            {
                product = productByLegacyId;
                includeReviewFragment = true;
            }
            else
            {
                // Earlier public review pages were keyed by EtsyReview.Id.
                // Those marketplace reviews are deliberately not injected into
                // first-party Review JSON-LD, but their historic public URLs
                // should resolve to the matching product rather than 404.
                var etsyReview = await _etsyReviewRepository.GetByIdAsync(reviewId);
                if (!string.IsNullOrWhiteSpace(etsyReview?.Sku))
                    product = await _productService.GetProductBySkuAsync(etsyReview.Sku);
            }
        }

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

        var target = $"/{targetLanguage.UniqueSeoCode}/{slug}";
        return LocalRedirectPermanent(includeReviewFragment ? $"{target}#productreviews" : target);
    }
}
