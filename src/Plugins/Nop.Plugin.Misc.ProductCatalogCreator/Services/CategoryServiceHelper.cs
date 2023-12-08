using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nop.Services.Catalog;

namespace Nop.Plugin.Misc.ProductCatalogCreator.Services
{
    public class CategoryServiceHelper
    {
        private readonly ICategoryService _categoryService;

        public CategoryServiceHelper(ICategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        public async Task<List<(int categoryId, string categoryName)>> GetCategoryListAsync()
        {
            var categories = await _categoryService.GetAllCategoriesAsync(showHidden: true);
            return categories.Select(c => (c.Id, c.Name)).ToList();
        }
    }
}
