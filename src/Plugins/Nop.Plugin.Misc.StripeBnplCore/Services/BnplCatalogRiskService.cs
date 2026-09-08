using System.Text;
using Nop.Services.Catalog;

namespace Nop.Plugin.Misc.StripeBnplCore.Services;

public sealed class BnplCatalogRiskService : IBnplCatalogRiskService
{
    private static readonly string[] RestrictedTerms =
    {
        "archery", "arrow", "arrows", "arrowhead", "arrowheads", "bodkin", "bodkins", "bow", "bows",
        "broadhead", "broadheads", "crossbow", "crossbows", "dagger", "daggers", "hunting", "knife", "knives",
        "spear", "spears", "sword", "swords", "weapon", "weapons"
    };

    private readonly ICategoryService _categoryService;
    private readonly IProductAttributeParser _productAttributeParser;
    private readonly IProductAttributeService _productAttributeService;
    private readonly IProductService _productService;
    private readonly IProductTagService _productTagService;

    public BnplCatalogRiskService(ICategoryService categoryService,
        IProductAttributeParser productAttributeParser,
        IProductAttributeService productAttributeService,
        IProductService productService,
        IProductTagService productTagService)
    {
        _categoryService = categoryService;
        _productAttributeParser = productAttributeParser;
        _productAttributeService = productAttributeService;
        _productService = productService;
        _productTagService = productTagService;
    }

    public async Task<string> FindRestrictedTermAsync(int productId, string attributesXml = null)
    {
        var product = await _productService.GetProductByIdAsync(productId);
        if (product == null || product.Deleted || !product.Published)
            return "unavailable product";

        var text = new StringBuilder()
            .Append(' ').Append(product.Name)
            .Append(' ').Append(product.Sku)
            .Append(' ').Append(product.ShortDescription)
            .Append(' ').Append(product.FullDescription);
        foreach (var mapping in await _categoryService.GetProductCategoriesByProductIdAsync(product.Id, showHidden: true))
            text.Append(' ').Append((await _categoryService.GetCategoryByIdAsync(mapping.CategoryId))?.Name);
        foreach (var tag in await _productTagService.GetAllProductTagsByProductIdAsync(product.Id))
            text.Append(' ').Append(tag.Name);

        // A provider decision applies to the whole nopCommerce product until
        // variant-level eligibility is modelled explicitly. Scan every defined
        // option, not only the value selected in the current cart. Otherwise a
        // safe selection (for example, "field point") could expose BNPL for a
        // product that also offers a prohibited broadhead/arrowhead option.
        foreach (var mapping in await _productAttributeService.GetProductAttributeMappingsByProductIdAsync(product.Id))
        {
            text.Append(' ').Append(mapping.TextPrompt)
                .Append(' ').Append(mapping.DefaultValue)
                .Append(' ').Append((await _productAttributeService
                    .GetProductAttributeByIdAsync(mapping.ProductAttributeId))?.Name);

            foreach (var value in await _productAttributeService.GetProductAttributeValuesAsync(mapping.Id))
            {
                text.Append(' ').Append(value.Name);

                // Associated-product options sometimes use a generic label such
                // as "Add item". Include the referenced catalog identity without
                // recursing so that the prohibited item cannot hide behind it.
                if (value.AssociatedProductId <= 0 || value.AssociatedProductId == product.Id)
                    continue;
                var associatedProduct = await _productService.GetProductByIdAsync(value.AssociatedProductId);
                if (associatedProduct != null && !associatedProduct.Deleted)
                    text.Append(' ').Append(associatedProduct.Name)
                        .Append(' ').Append(associatedProduct.Sku)
                        .Append(' ').Append(associatedProduct.ShortDescription)
                        .Append(' ').Append(associatedProduct.FullDescription);
            }
        }

        // Keep scanning the captured selection as well. This covers historical
        // option values that may no longer be present in the current mapping.
        if (!string.IsNullOrWhiteSpace(attributesXml))
            foreach (var value in await _productAttributeParser.ParseProductAttributeValuesAsync(attributesXml))
                text.Append(' ').Append(value.Name);

        var normalized = text.ToString().ToLowerInvariant();
        return RestrictedTerms.FirstOrDefault(term => ContainsWholeTerm(normalized, term));
    }

    private static bool ContainsWholeTerm(string text, string term)
    {
        var start = 0;
        while ((start = text.IndexOf(term, start, StringComparison.Ordinal)) >= 0)
        {
            var leftBoundary = start == 0 || !char.IsLetterOrDigit(text[start - 1]);
            var rightIndex = start + term.Length;
            var rightBoundary = rightIndex >= text.Length || !char.IsLetterOrDigit(text[rightIndex]);
            if (leftBoundary && rightBoundary)
                return true;
            start++;
        }
        return false;
    }
}
