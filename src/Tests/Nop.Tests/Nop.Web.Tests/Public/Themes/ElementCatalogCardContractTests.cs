using FluentAssertions;
using NUnit.Framework;

namespace Nop.Tests.Nop.Web.Tests.Public.Themes;

[TestFixture]
public class ElementCatalogCardContractTests
{
    [Test]
    public void ProductBox_reserves_square_media_and_uses_semantic_card_scope()
    {
        var productBox = ReadSource("src", "Presentation", "Nop.Web", "Themes", "Element", "Views", "Shared", "_ProductBox.cshtml");

        productBox.Should().Contain("catalog-product-card");
        productBox.Should().Contain("catalog-product-media");
        productBox.Should().NotContain("width=\"635\"");
        productBox.Should().NotContain("height=\"635\"");
    }

    [Test]
    public void Catalog_styles_keep_media_square_without_cropping_and_align_card_content()
    {
        var styles = ReadSource("src", "Presentation", "Nop.Web", "Themes", "Element", "Content", "css", "styles.css");
        var mobileStyles = ReadSource("src", "Presentation", "Nop.Web", "Themes", "Element", "Content", "css", "mobile.css");
        var head = ReadSource("src", "Presentation", "Nop.Web", "Themes", "Element", "Views", "Shared", "Head.cshtml");

        styles.Should().Contain(".catalog-product-card .catalog-product-media");
        styles.Should().Contain("aspect-ratio: 1 / 1");
        styles.Should().Contain("object-fit: contain");
        styles.Should().Contain("object-position: center");
        styles.Should().Contain(".catalog-product-card .prices");
        styles.Should().Contain(".product-details-page .full-description iframe");
        styles.Should().Contain("max-width: 100%");
        mobileStyles.Should().Contain(".catalog-product-card .product-title");
        mobileStyles.Should().Contain("height: 65px");
        mobileStyles.Should().Contain(".catalog-product-card .prices");
        mobileStyles.Should().Contain("overflow-wrap: anywhere");
        head.Should().Contain("Content/css/styles.css");
        head.Should().NotContain("Content/css/styles.css?v=");
    }

    [Test]
    public void Root_head_removes_known_stale_hood_asset_tags_from_custom_html()
    {
        var elementRoot = ReadSource("src", "Presentation", "Nop.Web", "Themes", "Element", "Views", "Shared", "_Root.Head.cshtml");
        var genericRoot = ReadSource("src", "Presentation", "Nop.Web", "Views", "Shared", "_Root.Head.cshtml");

        elementRoot.Should().Contain("hood-(?:third-party-gate-v6|v9-report-fixes)");
        genericRoot.Should().Contain("hood-(?:third-party-gate-v6|v9-report-fixes)");
        elementRoot.Should().Contain("<noscript\\b[^>]*>.*?</noscript>");
        genericRoot.Should().Contain("<noscript\\b[^>]*>.*?</noscript>");
        elementRoot.Should().Contain("headerCustomHtml = Regex.Replace(headerCustomHtml");
        elementRoot.Should().Contain("customHeadTags = Regex.Replace(seoSettings.CustomHeadTags");
        elementRoot.Should().Contain("display=optional");
        elementRoot.Should().NotContain("display=swap");
        elementRoot.Should().NotContain("<link rel=\"stylesheet\" href=\"/Themes/Element/Content/css/hood-third-party-gate-v6.css");
        genericRoot.Should().NotContain("<script src=\"/Themes/Element/Content/scripts/hood-third-party-gate-v6.js");
    }

    [Test]
    public void Category_template_registers_json_ld_through_shared_head_pipeline()
    {
        var category = ReadSource("src", "Presentation", "Nop.Web", "Themes", "Element", "Views", "Catalog", "CategoryTemplate.ProductsInGridOrLines.cshtml");

        category.Should().Contain("NopHtml.AddJsonLdParts(Model.JsonLd)");
        category.Should().NotContain("<script type=\"application/ld+json\">");
    }

    [Test]
    public void Product_review_overview_uses_provenance_filtered_rows()
    {
        var factory = ReadSource("src", "Presentation", "Nop.Web", "Factories", "ProductModelFactory.cs");
        var overview = factory.Substring(factory.IndexOf("PrepareProductReviewOverviewModelAsync", StringComparison.Ordinal));

        overview.Should().Contain("var eligibleReviews = await FilterEligibleNativeReviewsAsync(productReviews);");
        overview.Should().Contain("RatingSum = eligibleReviews.Sum(pr => pr.Rating)");
        overview.Should().Contain("TotalReviews = eligibleReviews.Count");
        overview.Should().NotContain("RatingSum = productReviews.Sum(pr => pr.Rating)");
        overview.Should().NotContain("TotalReviews = productReviews.Count");
    }

    [Test]
    public void Product_review_list_uses_the_same_filtered_rows_as_the_overview()
    {
        var factory = ReadSource("src", "Presentation", "Nop.Web", "Factories", "ProductModelFactory.cs");
        var reviews = factory.Substring(factory.IndexOf("PrepareProductReviewsModelAsync", StringComparison.Ordinal));

        reviews.Should().Contain("productReviews = await FilterEligibleNativeReviewsAsync(productReviews);");
        reviews.Should().Contain("GetCustomersByIdsAsync(customerIds)");
    }

    [Test]
    public void Review_picture_markup_defers_full_size_images_and_reserves_thumbnail_space()
    {
        var view = ReadSource("src", "Plugins", "Nop.Plugin.Widgets.CustomProductReviews", "Views", "_ProductReviewPictures.cshtml");
        var styles = ReadSource("src", "Plugins", "Nop.Plugin.Widgets.CustomProductReviews", "Content", "style.css");

        view.Should().Contain("class=\"review-photo-thumb\"");
        view.Should().Contain("width=\"150\" height=\"150\"");
        view.Should().Contain("loading=\"lazy\"");
        view.Should().NotContain("src=\"@picture.FullSizeImageUrl\"");
        styles.Should().Contain(".product-review-item .review-photo-thumb");
        styles.Should().Contain("object-fit: contain");
        styles.Should().Contain("object-position: center");
    }

    [Test]
    public void Review_picture_component_uses_request_cache_and_skips_missing_media()
    {
        var component = ReadSource("src", "Plugins", "Nop.Plugin.Widgets.CustomProductReviews", "Components", "ProductReviewPicturesViewComponent.cs");

        component.Should().Contain("IShortTermCacheManager");
        component.Should().Contain("_shortTermCacheManager.GetAsync");
        component.Should().Contain("if (picture == null)");
        component.Should().Contain("if (mapping.PictureId is not > 0)");
    }

    [Test]
    public void Catalog_old_price_requires_effective_old_price_above_current_minimum()
    {
        var factory = ReadSource("src", "Presentation", "Nop.Web", "Factories", "ProductModelFactory.cs");

        factory.Should().Contain("if (oldPriceBase > finalPriceWithoutDiscountBase && oldPriceBase > decimal.Zero)");
        factory.Should().NotContain("if (finalPriceWithoutDiscountBase != oldPriceBase && oldPriceBase > decimal.Zero)");
    }

    [Test]
    public void Category_template_reserves_carousel_footprint_before_initialization()
    {
        var category = ReadSource("src", "Presentation", "Nop.Web", "Themes", "Element", "Views", "Catalog", "CategoryTemplate.ProductsInGridOrLines.cshtml");
        var styles = ReadSource("src", "Presentation", "Nop.Web", "Themes", "Element", "Content", "css", "styles.css");

        category.Should().Contain("owl-carousel owl-theme category-carousel-pending");
        category.Should().Contain("$('.sub-category-grid .owl-carousel')");
        category.Should().Contain("owl.removeClass('category-carousel-pending')");
        styles.Should().Contain(".sub-category-grid .owl-carousel.category-carousel-pending");
        styles.Should().Contain("flex-basis: 100%");
    }

    private static string ReadSource(params string[] parts)
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src", "Presentation", "Nop.Web")))
            directory = directory.Parent;

        directory.Should().NotBeNull("the source contract tests must run from a repository checkout");
        var path = Path.Combine(new[] { directory!.FullName }.Concat(parts).ToArray());
        File.Exists(path).Should().BeTrue("the expected source file should be present: {0}", path);
        return File.ReadAllText(path);
    }
}
