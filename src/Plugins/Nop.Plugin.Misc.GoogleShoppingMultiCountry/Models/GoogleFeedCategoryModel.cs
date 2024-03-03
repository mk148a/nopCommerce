using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Misc.GoogleShoppingMultiCountry.Models
{
    public record GoogleFeedCategoryModel : BaseNopEntityModel
    {
        #region Ctor

        public GoogleFeedCategoryModel()
        {
            GoogleFeedCategoryListSearchModel = new GoogleFeedCategorySearchModel();
        }

        #endregion

        public int CategoryId { get; set; }

        [NopResourceDisplayName("Plugins.Misc.GoogleShoppingMultiCountry.Products.CategoryName")]
        public string CategoryName { get; set; }

        [NopResourceDisplayName("Plugins.Misc.GoogleShoppingMultiCountry.Products.GoogleCategory")]
        public string GoogleCategory { get; set; }
        [NopResourceDisplayName("Plugins.Misc.GoogleShoppingMultiCountry.Products.GoogleCategoryId")]
        public int GoogleCategoryId { get; set; }


        public GoogleFeedCategorySearchModel GoogleFeedCategoryListSearchModel { get; set; }
    }
}
