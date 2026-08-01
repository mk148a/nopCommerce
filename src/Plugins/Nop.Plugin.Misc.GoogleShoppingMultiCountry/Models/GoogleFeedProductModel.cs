using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Misc.GoogleShoppingMultiCountry.Models
{
    public record GoogleFeedProductModel : BaseNopEntityModel
    {
        #region Ctor

        public GoogleFeedProductModel()
        {
            GoogleFeedProductListSearchModel = new GoogleFeedProductSearchModel();
        }

        #endregion

        public int ProductId { get; set; }

        [NopResourceDisplayName("Plugins.Misc.GoogleShoppingMultiCountry.Products.ProductName")]
        public string ProductName { get; set; }

        [NopResourceDisplayName("Plugins.Misc.GoogleShoppingMultiCountry.Products.GoogleCategory")]
        public string GoogleCategory { get; set; }

        [NopResourceDisplayName("Plugins.Misc.GoogleShoppingMultiCountry.Products.Gender")]
        public string Gender { get; set; }

        [NopResourceDisplayName("Plugins.Misc.GoogleShoppingMultiCountry.Products.AgeGroup")]
        public string AgeGroup { get; set; }

        [NopResourceDisplayName("Plugins.Misc.GoogleShoppingMultiCountry.Products.Color")]
        public string Color { get; set; }

        [NopResourceDisplayName("Plugins.Misc.GoogleShoppingMultiCountry.Products.Size")]
        public string GoogleSize { get; set; }

        [NopResourceDisplayName("Plugins.Misc.GoogleShoppingMultiCountry.CustomGoods")]
        public bool CustomGoods { get; set; }
        [NopResourceDisplayName("Plugins.Misc.GoogleShoppingMultiCountry.Products.LanguageId")]
        public int LanguageId{ get; set; }
        public GoogleFeedProductSearchModel GoogleFeedProductListSearchModel { get; set; }
    }
}
