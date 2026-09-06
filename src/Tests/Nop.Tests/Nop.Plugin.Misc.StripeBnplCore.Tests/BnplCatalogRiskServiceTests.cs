using Moq;
using Nop.Core.Domain.Catalog;
using Nop.Plugin.Misc.StripeBnplCore.Services;
using Nop.Services.Catalog;
using NUnit.Framework;

namespace Nop.Tests.Nop.Plugin.Misc.StripeBnplCore.Tests;

[TestFixture]
public class BnplCatalogRiskServiceTests
{
    [TestCase("Broadhead point", "broadhead")]
    [TestCase("Arrowhead point", "arrowhead")]
    public async Task UnselectedRestrictedOptionBlocksTheWholeProduct(string unselectedOption, string expectedTerm)
    {
        var fixture = CreateFixture(unselectedOption);

        var term = await fixture.Service.FindRestrictedTermAsync(10, "<Attributes>safe-field-point</Attributes>");

        Assert.That(term, Is.EqualTo(expectedTerm));
        fixture.AttributeService.Verify(service => service.GetProductAttributeValuesAsync(700), Times.Once);
        fixture.AttributeParser.Verify(parser => parser.ParseProductAttributeValuesAsync(
            "<Attributes>safe-field-point</Attributes>", 0), Times.Once);
    }

    [Test]
    public async Task AdminScanWithoutCartSelectionStillFindsRestrictedOption()
    {
        var fixture = CreateFixture("Arrowhead point");

        var term = await fixture.Service.FindRestrictedTermAsync(10);

        Assert.That(term, Is.EqualTo("arrowhead"));
        fixture.AttributeParser.Verify(parser => parser.ParseProductAttributeValuesAsync(
            It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Test]
    public async Task GenericAssociatedOptionCannotHideRestrictedCatalogItem()
    {
        var fixture = CreateFixture("Add item", associatedProductId: 20);
        fixture.ProductService.Setup(service => service.GetProductByIdAsync(20)).ReturnsAsync(new Product
        {
            Id = 20,
            Name = "Decorative sword prop",
            Sku = "PROP-20",
            Published = true
        });

        var term = await fixture.Service.FindRestrictedTermAsync(10, "<Attributes>safe-field-point</Attributes>");

        Assert.That(term, Is.EqualTo("sword"));
    }

    private static CatalogRiskFixture CreateFixture(string unselectedOption, int associatedProductId = 0)
    {
        var categoryService = new Mock<ICategoryService>();
        categoryService.Setup(service => service.GetProductCategoriesByProductIdAsync(10, true))
            .ReturnsAsync(Array.Empty<ProductCategory>());

        var attributeParser = new Mock<IProductAttributeParser>();
        attributeParser.Setup(parser => parser.ParseProductAttributeValuesAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(new List<ProductAttributeValue>
            {
                new() { Id = 701, ProductAttributeMappingId = 700, Name = "Field point" }
            });

        var attributeService = new Mock<IProductAttributeService>();
        attributeService.Setup(service => service.GetProductAttributeMappingsByProductIdAsync(10))
            .ReturnsAsync(new List<ProductAttributeMapping>
            {
                new()
                {
                    Id = 700,
                    ProductId = 10,
                    ProductAttributeId = 70,
                    TextPrompt = "Tip style"
                }
            });
        attributeService.Setup(service => service.GetProductAttributeByIdAsync(70))
            .ReturnsAsync(new ProductAttribute { Id = 70, Name = "Tip style" });
        attributeService.Setup(service => service.GetProductAttributeValuesAsync(700))
            .ReturnsAsync(new List<ProductAttributeValue>
            {
                new() { Id = 701, ProductAttributeMappingId = 700, Name = "Field point" },
                new()
                {
                    Id = 702,
                    ProductAttributeMappingId = 700,
                    Name = unselectedOption,
                    AssociatedProductId = associatedProductId
                }
            });

        var productService = new Mock<IProductService>();
        productService.Setup(service => service.GetProductByIdAsync(10)).ReturnsAsync(new Product
        {
            Id = 10,
            Name = "Leather costume accessory",
            Sku = "SAFE-10",
            Published = true
        });

        var productTagService = new Mock<IProductTagService>();
        productTagService.Setup(service => service.GetAllProductTagsByProductIdAsync(10))
            .ReturnsAsync(Array.Empty<ProductTag>());

        return new CatalogRiskFixture(
            new BnplCatalogRiskService(categoryService.Object, attributeParser.Object, attributeService.Object,
                productService.Object, productTagService.Object),
            attributeParser,
            attributeService,
            productService);
    }

    private sealed record CatalogRiskFixture(
        BnplCatalogRiskService Service,
        Mock<IProductAttributeParser> AttributeParser,
        Mock<IProductAttributeService> AttributeService,
        Mock<IProductService> ProductService);
}
