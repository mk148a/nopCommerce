using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DocumentFormat.OpenXml;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Plugin.Misc.ProductCatalogCreator.Areas.Admin.Models;
using Nop.Plugin.Misc.ProductCatalogCreator.Services;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Misc.ProductCatalogCreator.Areas.Admin.Controllers
{
    [Area("Admin")]
    [AuthorizeAdmin] // Admin yetkilendirme
    public class ProductCatalogController : Controller
    {
        private readonly ProductCatalogService _productCatalogService;

        public ProductCatalogController(ProductCatalogService productCatalogService)
        {
            _productCatalogService = productCatalogService;
        }

        public async Task<IActionResult> Index()
        {
            var categories = await _productCatalogService.GetAllCategoriesAsync();
            var model = new ProductCatalogModel
            {
                // Modeli doldur
                AvailableCategories = categories.Select(c => new SelectListItem
                {
                    Text = c.Name,
                    Value = c.Id.ToString()
                }).ToList()
            };
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Index(ProductCatalogModel model)
        {
          

            var products = await _productCatalogService.GetProductsByCategoryAsync(model.SelectedCategoryIds.ToArray());

            // Ürünleri ProductModel listesine dönüştür

            model.Products = new List<ProductModel>();

            foreach (var product in products)
            {
                var categoriesInfo = await _productCatalogService.GetCategoriesInfoByProductId(product.Id);
                var productImages = await _productCatalogService.GetProductImagesByProductId(product.Id);
                var productModel = new ProductModel
                {
                    ProductId = product.Id,
                    CategoryIds = categoriesInfo.Select(x => x.Item1).ToList(),
                    CategoryNames = categoriesInfo.Select(x => x.Item2).ToList(),
                    Sku = product.Sku,
                    Price = $"{product.Price:C}",
                    Title = product.Name,
                    ShortDescription = product.ShortDescription,
                    PictureUrl1 = productImages.Count > 0 ? productImages[0] : "",
                    PictureUrl2 = productImages.Count > 1 ? productImages[1] : "",
                    PictureUrl3 = productImages.Count > 2 ? productImages[2] : "",
                    PictureUrl4 = productImages.Count > 3 ? productImages[3] : "",
            };

                model.Products.Add(productModel);
            }

            // Kategorileri SelectListItem olarak yeniden dönüştür
            var categories = await _productCatalogService.GetAllCategoriesAsync();
            model.AvailableCategories = categories.Select(c => new SelectListItem
            {
                Text = c.Name,
                Value = c.Id.ToString(),
                Selected = model.SelectedCategoryIds.Contains(c.Id)
            }).ToList();

            return View(model);
        }
        [HttpPost]
        public async Task<IActionResult> CreateCatalog(ProductCatalogModel model)
        {
            model.Products = (await _productCatalogService.GetProductModelsByIdAsync(model.SelectedProductIds.ToArray())).ToList();
            List<CatalogCategory> catalogCategories = new List<CatalogCategory>();

            foreach (var product in model.Products)
            {
                foreach (var categoryName in product.CategoryNames)
                {
                    var categories = categoryName.Split(" > ");
                    var currentLevel = catalogCategories;

                    for (int i = 0; i < categories.Length; i++)
                    {
                        var category = currentLevel.FirstOrDefault(c => c.CategoryName == categories[i]);
                        if (category == null)
                        {
                            category = new CatalogCategory { CategoryName = categories[i] };
                            currentLevel.Add(category);
                        }

                        if (i == categories.Length - 1)
                        {
                            if (category.Products==null)
                            {
                                category.Products = new List<ProductModel>();
                            }
                            category.Products.Add(product);
                        }
                        else
                        {
                            if (category.Subcategories == null)
                            {
                                category.Subcategories = new List<CatalogCategory>();
                            }
                            currentLevel = category.Subcategories;
                        }
                    }
                }
            }

            var pdfBytes = _productCatalogService.CreatePdfCatalog(catalogCategories);

            // Şimdi catalogCategories listesini kullanarak PDF oluşturma işlemine geçebiliriz.

            return File(pdfBytes, "application/pdf");
        }
    }
}
