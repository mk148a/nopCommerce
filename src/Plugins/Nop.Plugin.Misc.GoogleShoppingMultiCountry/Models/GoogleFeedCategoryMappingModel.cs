using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nop.Plugin.Misc.GoogleShoppingMultiCountry.Domains;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;
using Nop.Core.Domain.Catalog;

namespace Nop.Plugin.Misc.GoogleShoppingMultiCountry.Models
{
    public record GoogleFeedCategoryMappingModel : BaseNopEntityModel
    {
        #region Ctor

        public GoogleFeedCategoryMappingModel()
        {
            GoogleFeedCategoryListSearchModel = new GoogleFeedCategorySearchModel();
            CategoryGoogleTaxonomyRecordMappings = new List<CategoryGoogleTaxonomyRecordMapping>();
        }

        #endregion

        public IList<Category> Categories { get; set; }
        public IList<CategoryGoogleTaxonomyRecordMapping> CategoryGoogleTaxonomyRecordMappings { get; set; }

        public GoogleFeedCategorySearchModel GoogleFeedCategoryListSearchModel { get; set; }
    }
}
