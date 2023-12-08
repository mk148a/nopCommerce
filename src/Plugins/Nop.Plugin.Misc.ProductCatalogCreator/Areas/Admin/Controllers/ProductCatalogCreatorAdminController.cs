using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.Misc.ProductCatalogCreator.Services;
using Nop.Web.Areas.Admin.Controllers;

namespace Nop.Plugin.Misc.ProductCatalogCreator.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class ProductCatalogCreatorAdminController : BaseAdminController
    {
        private readonly ProductServiceHelper _productServiceHelper;

        public ProductCatalogCreatorAdminController(ProductServiceHelper productServiceHelper)
        {
            _productServiceHelper = productServiceHelper;
        }

        public async Task<IActionResult> ProductList()
        {
            var products = await _productServiceHelper.GetAllProductsAsync();
            return View("~/Plugins/Misc.ProductCatalogCreator/Areas/Admin/Views/ProductCatalogCreatorAdmin/ProductList.cshtml", products);
        }
    }
}
