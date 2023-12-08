using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nop.Plugin.Misc.ProductCatalogCreator.Areas.Admin.Models
{
    public class CatalogCategory
    {
        public string CategoryName { get; set; }
        public List<ProductModel> Products { get; set; }
        public List<CatalogCategory> Subcategories { get; set; }
    }
}
