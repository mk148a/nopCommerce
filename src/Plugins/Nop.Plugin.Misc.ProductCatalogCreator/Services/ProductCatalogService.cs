using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LinqToDB.Common;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Plugin.Misc.ProductCatalogCreator.Areas.Admin.Models;
using Nop.Services.Catalog;
using Nop.Services.Media;
using Microsoft.AspNetCore.Mvc;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;

namespace Nop.Plugin.Misc.ProductCatalogCreator.Services
{
    public class ProductCatalogService
    {
        private readonly IProductService _productService;
        private readonly ICategoryService _categoryService;
        private readonly IPictureService _pictureService;
        private readonly IWebHelper _webHelper;

        public ProductCatalogService(IProductService productService, ICategoryService categoryService,
            IPictureService pictureService, IWebHelper webHelper)
        {
            _productService = productService;
            _categoryService = categoryService;
            _pictureService = pictureService;
            _webHelper = webHelper;
        }

        public async Task<IEnumerable<Category>> GetAllCategoriesAsync()
        {
            // Kategorileri getir
            var categories = await _categoryService.GetAllCategoriesAsync(showHidden: true);
            return categories;
        }

        public async Task<IEnumerable<Product>> GetProductsByCategoryAsync(int[] categoryIds)
        {
            // Belirli kategorilere göre ürünleri getir
            var products =
                await _productService.SearchProductsAsync(categoryIds: categoryIds.ToList(), showHidden: true);
            return products;


        }
        public async Task<List<ProductModel>> GetProductModelsByIdAsync(int[] productIds)
        {
            // Belirli idlere göre ürünleri getir
            var products =
                await _productService.GetProductsByIdsAsync(productIds);
            List<ProductModel> result = new List<ProductModel>();
            foreach (var product in products)
            {
                var categoriesInfo = await GetCategoriesInfoByProductId(product.Id);
                var productImages = await GetProductImagesByProductId(product.Id);
                var productModel = new ProductModel
                {
                    ProductId = product.Id,
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
                result.Add(productModel);
            }

            return result;


        }

        public async Task<IEnumerable<(int, string)>> GetCategoriesInfoByProductId(int productId)
        {
            var categoryInfoList = new List<(int, string)>();

            var productCategories = await _categoryService.GetProductCategoriesByProductIdAsync(productId);


            foreach (var productCategory in productCategories)
            {
                categoryInfoList.Add((productCategory.CategoryId,
                    (await _categoryService.GetCategoryByIdAsync(productCategory.CategoryId))?.Name ?? ""));
            }

            return categoryInfoList;
        }

        public async Task<List<string>> GetProductImagesByProductId(int productId, int maxImages = 4)
        {
            var pictureList = (await _pictureService.GetPicturesByProductIdAsync(productId)).Take(4);
            var imageLinks = new List<string>();

            foreach (var picture in pictureList)
            {
                var seName = picture.SeoFilename;
                var pictureId = picture.Id;
                var mimeType = picture.MimeType;

                var sb = new StringBuilder();
                sb.Append(_webHelper.GetStoreLocation());
                sb.Append("images/thumbs/");
                sb.Append(pictureId.ToString().PadLeft(7, '0'));
                sb.Append("_" + seName);
                sb.Append("." + mimeType.Replace("image/", ""));

                imageLinks.Add(sb.ToString());
            }

            return imageLinks;
        }


        public byte[] CreatePdfCatalog(List<CatalogCategory> catalogCategories)
        {
            using (var document = new PdfDocument())
            {
                // İlk sayfa (index)
                var indexPage = document.AddPage();
                var indexGraphics = XGraphics.FromPdfPage(indexPage);
                indexGraphics.DrawString("Index", new XFont("Arial", 16), XBrushes.Black, new XPoint(100, 100));

                // Diğer sayfalar
                foreach (var category in catalogCategories)
                {
                    var page = document.AddPage();
                    var graphics = XGraphics.FromPdfPage(page);
                    int yPosition = 100; // Başlangıç y koordinatı

                    // Kategori adını yazdır
                    graphics.DrawString(category.CategoryName, new XFont("Arial", 14), XBrushes.Black, new XPoint(100, yPosition));
                    yPosition += 40; // Kategori adından sonra y koordinatını artır


                    // Alt kategorileri ve ürünleri ekle
                    if (!category.Subcategories.IsNullOrEmpty())
                    {
                        foreach (var subCategory in category.Subcategories)
                        {
                            graphics.DrawString(subCategory.CategoryName, new XFont("Arial", 12), XBrushes.Black, new XPoint(120, yPosition));
                            yPosition += 40; // Alt kategori adından sonra y koordinatını artır

                            // Ürünleri ekle
                            if (!subCategory.Products.IsNullOrEmpty())
                            {
                                foreach (var product in subCategory.Products)
                                {
                                    graphics.DrawString(product.Title, new XFont("Arial", 10), XBrushes.Black, new XPoint(140, yPosition));
                                    yPosition += 20; // Üründen sonra y koordinatını artır
                                }
                            }
                        }
                    }
                    else
                    {
                        // Ürünleri burada ekleyebilirsiniz
                        foreach (var product in category.Products)
                        {
                            graphics.DrawString(product.Title, new XFont("Arial", 10), XBrushes.Black, new XPoint(140, yPosition));
                            yPosition += 20; // Üründen sonra y koordinatını artır
                        }
                    }
                   
                }

                // PDF dosyasını kaydet
                using (MemoryStream stream = new MemoryStream())
                {
                    document.Save(stream, false);
                    byte[] pdfBytes = stream.ToArray();

                    // Şimdi pdfBytes byte dizisini döndürebilirsiniz
                    return pdfBytes;
                }
            }
        }




    }
}
