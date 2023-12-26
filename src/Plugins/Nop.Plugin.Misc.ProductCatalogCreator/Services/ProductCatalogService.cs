using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HTMLQuestPDF.Extensions;
using LinqToDB.Common;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Plugin.Misc.ProductCatalogCreator.Areas.Admin.Models;
using Nop.Services.Catalog;
using Nop.Services.Media;
using Microsoft.AspNetCore.Mvc;
using QuestPDF.Fluent;


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

                string htmlTemplate = "<!DOCTYPE html>\r\n<html style=\"font-size: 16px;\" >\r\n<head>\r\n<meta http-equiv=\"content-type\" content=\"text/html; charset=UTF-8\">  \r\n<style>\r\n       .body-style {\r\n  font-size: 1rem;\r\n  line-height: 1.6;\r\n   font-family: 'Kingthings Exeter', sans-serif;\r\n   \r\n}\r\n.body-style h1,\r\n.body-style h2,\r\n.body-style h3,\r\n.body-style h4,\r\n.body-style h5,\r\n.body-style h6 {\r\n  padding: 0;\r\n}\r\n\r\nh1,\r\nh2,\r\nh3,\r\nh4,\r\nh5,\r\nh6 {\r\n  margin-top: 0;\r\n  margin-bottom: 0.5rem;\r\n  line-height: 1.2;\r\n  font-weight: 500;\r\n  color: inherit;\r\n}\r\nh1 {\r\n  font-size: 2.25rem;\r\n}\r\nh2 {\r\n  font-size: 1.5rem;\r\n}\r\nh3 {\r\n  font-size: 1.25rem;\r\n}\r\nh4 {\r\n  font-size: 1.25rem;\r\n}\r\nh5 {\r\n  font-size: 1.125rem;\r\n}\r\nh6 {\r\n  font-size: 1.125rem;\r\n}\r\np {\r\n  margin-top: 0;\r\n  padding: 0;\r\n  margin-bottom: 0.5rem;\r\n}\r\n\r\n.clearfix:after,\r\n.clearfix:before {\r\n  content: '';\r\n  display: table;\r\n}\r\n.clearfix:after {\r\n  clear: both;\r\n}\r\n\r\n\r\n\r\n\r\n\r\n.image-item {\r\n  display: flex;\r\n  flex-direction: column;\r\n  margin: 5px; /* Dikey ve yatay boşluk için */\r\n}\r\n\r\n\r\n.product-image {\r\n  width: auto; /* Konteynerin genişliğine göre otomatik ayarlanır */\r\n  height: 150px; /* Yükseklik sabit */\r\n  object-fit: cover; /* Görselin tamamını kutuya sığdır */\r\n  margin-bottom: 10px; /* Alt görsel boşluğu */\r\n}\r\n}\r\n\r\n.expanded-image {\r\n  position: absolute !important;\r\n  left: 0;\r\n  top: 0;\r\n  width: 100%;\r\n  height: 100%;\r\n}\r\n.full-width {\r\n  width: auto !important;\r\n  margin-left: 0 !important;\r\n  margin-right: 0 !important;\r\n   margin-top: 0 !important;\r\n   margin-bottom: 0 !important;\r\n}\r\n\r\n.product-container {\r\ndisplay: grid;\r\n  grid-template-columns: repeat(auto-fill, minmax(100mm, 1fr)); /* Her kartın en az 100mm genişliğinde olmasını sağlar ve sayfaya sığdırır */\r\n  grid-gap: 10px;\r\n  padding: 10px;\r\n  margin: auto;\r\n}\r\n\r\n.product-card {\r\nborder: 1px solid #ccc;\r\n  padding: 10px;\r\n  box-sizing: border-box; /* Padding ve border dahil olmak üzere genişliği sabit tutar */\r\n  overflow: hidden; /* İçeriğin taşmasını önler */\r\n  border-color: black;\r\n}\r\n\r\n.product-images {\r\n width: 100%; /* Resmi iç div'e sığdırır */\r\n  height: auto; /* Resmin orijinal oranını korur */\r\n  object-fit: contain;\r\n}\r\n\r\n.product-images img {\r\n  width: 150px;\r\n  height: 150px;\r\n  object-fit: scale-down;\r\n}\r\n\r\n.product-title,\r\n.product-sku,\r\n.product-description {\r\n margin-top: 10px;\r\n  word-wrap: break-word; /* Uzun kelimelerin satır sonunda kırılmasını sağlar */\r\n  white-space: normal; /* Metnin doğal akışını korur ve gerektiğinde yeni satıra geçer */\r\n  overflow-wrap: break-word; /* Taşan kelimelerin satır sonunda kırılmasını sağlar */\r\n  max-width: 100%; /* Metnin maksimum genişliğini sınırlar */\r\n}\r\nbody {\r\n  width: 210mm;\r\n  min-height: 297mm;\r\n  margin: 0 auto;\r\n  box-shadow: 0 0 10px rgba(0, 0, 0, 0.5);\r\n  background: white;  \r\n    overflow: hidden; /* Taşmaları önler */\r\n}\r\n\r\n\r\n.main-sheet {\r\n  min-height: 774px;\r\n   background-image: url('pdfbackground.png');\r\n  background-size: fit; /* Görselin kartı kaplamasını sağlar */\r\n  background-position: center; /* Görselin merkezden başlamasını sağlar */\r\n}\r\n.category-title {\r\n  font-size: 3rem;\r\n  font-weight: 100; \r\n  white-space: nowrap;\r\n  text-overflow: ellipsis; \r\n   text-align: center; /* Metni yatay olarak ortalar */\r\n    margin: auto; /* Üst ve alt boşlukları eşitler */\r\n    width: 100%; /* Genişliği konteynere eşitler */\r\n}\r\n .product-title {\r\n  text-transform: uppercase;\r\n  letter-spacing: 4px;\r\n  font-weight: 700; \r\n  overflow-wrap: break-word;  \r\n  text-overflow: ellipsis;\r\n  margin-top: 10px;\r\n\r\n}\r\n\r\n.product-sku {\r\n  overflow-wrap: break-word;  \r\n  text-overflow: ellipsis;\r\n  margin-top: 10px;\r\n  display: inline-block;    \r\n  color: black;\r\n  font-weight: 800;\r\n}\r\n.product-description {\r\n  overflow-wrap: break-word;  \r\n  text-overflow: ellipsis;\r\n  margin-top: 10px; \r\n  display: inline-block;    \r\n  color: black;\r\n  font-weight: 200;\r\n   font-style: italic;\r\n}\r\n.product-price {\r\n  overflow-wrap: break-word;  \r\n  text-overflow: ellipsis;\r\n  margin-top: 10px; \r\n  font-weight: 800;\r\n  color:#007c5a;\r\n  text-align: right;\r\n}\r\n.product-images-gallery {\r\n  margin-top: 0px;\r\n  margin-bottom: 0px;\r\n  height: 320px;\r\n  padding-bottom:20px;\r\n}\r\n.images-grid {\r\n  display: grid;\r\n  grid-template-columns: repeat(2, 1fr); /* 2 sütun tanımı */\r\n  grid-auto-rows: auto; /* Otomatik satır yüksekliği */\r\n  grid-gap: 10px; /* Hücreler arası boşluk */\r\n}\r\n\r\n\r\n    </style> \r\n\r\n    \r\n    \r\n</head>\r\n  <body class=\"body-style\">\r\n    \r\n    <section class=\"clearfix\" >\r\n      <div class=\"clearfix  main-sheet\">\t";
                string finalHtml = "";
                // İlk sayfa (index)
               

                // Diğer sayfalar
                finalHtml = htmlTemplate;
                foreach (var category in catalogCategories)
                {

                    // Alt kategorileri ve ürünleri ekle
                    if (!category.Subcategories.IsNullOrEmpty())
                    {
                        foreach (var subCategory in category.Subcategories)
                        {
                            string categoriesHtml = " <h2 class=\"category-title\">" + subCategory.CategoryName + "</h2>";
                            finalHtml += categoriesHtml;
                            // Ürünleri burada ekleyebilirsiniz
                            if (!subCategory.Products.IsNullOrEmpty())
                            {
                                string productHtml = "<div class=\"product-container\">";
                                foreach (var product in subCategory.Products)
                                {
                                    productHtml += "<div class=\"product-card\">" + "<h5 class=\"product-title\">" +
                                                   product.Title + "</h5>";
                                    productHtml += " <div class=\"full-width  product-images-gallery\" >" +
                                                   " <div class=\"images-grid\">" +
                                                   " <div class=\" image-item\">" +
                                                   "<img class=\"product-image expanded-image\" src=\"" +
                                                   product.PictureUrl1 + "\">" +
                                                   " </div>" +
                                                   " <div class=\" image-item\">" +
                                                   "<img class=\"product-image expanded-image\" src=\"" +
                                                   product.PictureUrl2 + "\">" +
                                                   " </div>" +
                                                   " <div class=\" image-item\">" +
                                                   "<img class=\"product-image expanded-image\" src=\"" +
                                                   product.PictureUrl3 + "\">" +
                                                   " </div>" +
                                                   " <div class=\" image-item\">" +
                                                   "<img class=\"product-image expanded-image\" src=\"" +
                                                   product.PictureUrl4 + "\">" +
                                                   " </div>" +
                                                   " </div>" +
                                                   " </div>" +
                                                   "  <p class=\"product-sku\">" + product.Sku + "</p>" +
                                                   " <p class=\"product-description\">" + product.ShortDescription +
                                                   "</p>" +
                                                   "<p class=\"product-price\">" + product.Price + "</p>" +
                                                   "</div>";

                                }

                                productHtml +=
                                    " </div>\r\n\t  </div>\r\n    </section>\r\n    \r\n    \r\n  \r\n</body></html>";
                                finalHtml += productHtml;
                            }


                        }
                    }
                    else
                    {
                        string categoriesHtml = " <h2 class=\"category-title\">" + category.CategoryName + "</h2>";
                        finalHtml += categoriesHtml;
                        // Ürünleri burada ekleyebilirsiniz
                        if (!category.Products.IsNullOrEmpty())
                        {
                            string productHtml = "<div class=\"product-container\">";
                            foreach (var product in category.Products)
                            {
                                productHtml += "<div class=\"product-card\">" + "<h5 class=\"product-title\">" +
                                               product.Title + "</h5>";
                                productHtml += " <div class=\"full-width  product-images-gallery\" >" +
                                               " <div class=\"images-grid\">" +
                                               " <div class=\" image-item\">" +
                                               "<img class=\"product-image expanded-image\" src=\"" +
                                               product.PictureUrl1 + "\">" +
                                               " </div>" +
                                               " <div class=\" image-item\">" +
                                               "<img class=\"product-image expanded-image\" src=\"" +
                                               product.PictureUrl2 + "\">" +
                                               " </div>" +
                                               " <div class=\" image-item\">" +
                                               "<img class=\"product-image expanded-image\" src=\"" +
                                               product.PictureUrl3 + "\">" +
                                               " </div>" +
                                               " <div class=\" image-item\">" +
                                               "<img class=\"product-image expanded-image\" src=\"" +
                                               product.PictureUrl4 + "\">" +
                                               " </div>" +
                                               " </div>" +
                                               " </div>" +
                                               "  <p class=\"product-sku\">" + product.Sku + "</p>" +
                                               " <p class=\"product-description\">" + product.ShortDescription +
                                               "</p>" +
                                               "<p class=\"product-price\">" + product.Price + "</p>" +
                                               "</div>";

                            }

                            productHtml +=
                                " </div>\r\n\t  </div>\r\n    </section>\r\n    \r\n    \r\n  \r\n</body></html>";
                            finalHtml += productHtml;
                        }
                    }

                }

                // PDF dosyasını kaydet
                using (MemoryStream stream = new MemoryStream())
                {
                    Byte[] pdfBytes = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Content().Column(col =>
                        {
                            col.Item().HTML(handler =>
                            {
                                handler.SetHtml(finalHtml);
                            });
                        });
                    });
                }).GeneratePdf();
             
                     
                    
                    // Şimdi pdfBytes byte dizisini döndürebilirsiniz
                    return pdfBytes;
                }
            
        }




    }
}
