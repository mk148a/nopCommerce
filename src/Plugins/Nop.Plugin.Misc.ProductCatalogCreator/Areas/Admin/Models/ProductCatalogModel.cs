    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc.Rendering;

    namespace Nop.Plugin.Misc.ProductCatalogCreator.Areas.Admin.Models
    {
        public class ProductCatalogModel
        {
            public ProductCatalogModel()
            {
                Products = new List<ProductModel>();
                AvailableCategories = new List<SelectListItem>();
                SelectedCategoryIds = new List<int>();
                SelectedProductIds = new List<int>();
        }

            public List<ProductModel> Products { get; set; }
            public List<SelectListItem> AvailableCategories { get; set; }
        public List<int> SelectedCategoryIds { get; set; }
        public List<int> SelectedProductIds { get; set; }
        public IFormFile TemplateFile { get; set; }
        public IFormFile FontFile { get; set; }
        public bool IncludeSKU { get; set; }
        public bool IncludeTitle { get; set; }
        public bool IncludePrice { get; set; }
        public bool IncludeShortDescription { get; set; }
        public bool IncludeImages { get; set; }
    }

        public class ProductModel
        {
            public int ProductId { get; set; }
            public List<int> CategoryIds { get; set; }
            public List<string> CategoryNames { get; set; }
            public string Sku { get; set; }
            public string Price { get; set; }
            public string Title { get; set; }
            public string ShortDescription { get; set; }
            public string PictureUrl1 { get; set; }
            public string PictureUrl2 { get; set; }
            public string PictureUrl3 { get; set; }
            public string PictureUrl4 { get; set; }
        }
    }
