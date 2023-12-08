using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nop.Services.Catalog;
using Nop.Services.Media;

namespace Nop.Plugin.Misc.ProductCatalogCreator.Services
{
    public class ProductServiceHelper
    {
        private readonly IProductService _productService;
        private readonly IPictureService _pictureService;

        public ProductServiceHelper(IProductService productService, IPictureService pictureService)
        {
            _productService = productService;
            _pictureService = pictureService;
        }

        public async Task<IEnumerable<ProductModel>> GetAllProductsAsync()
        {
            var products = await _productService.SearchProductsAsync(showHidden: true);
            var productModels = new List<ProductModel>();

            foreach (var product in products)
            {
                var defaultProductPicture = (await _pictureService.GetPicturesByProductIdAsync(product.Id, 1)).FirstOrDefault();
                var pictureUrl = (await _pictureService.GetPictureUrlAsync(defaultProductPicture)).Url;

                var productModel = new ProductModel
                {
                    Id = product.Id,
                    Name = product.Name,
                    FullDescription = product.FullDescription,
                    ShortDescription = product.ShortDescription,
                    Sku = product.Sku,
                    Price = product.Price,
                    PictureUrl = pictureUrl
                };

                productModels.Add(productModel);
            }

            return productModels;
        }
    }

    public class ProductModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string FullDescription { get; set; }
        public string ShortDescription { get; set; }
        public string Sku { get; set; }
        public decimal Price { get; set; }
        public string PictureUrl { get; set; }
    }
}
