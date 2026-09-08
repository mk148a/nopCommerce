using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Misc.GoogleShoppingMultiCountry.Models
{
    public record SearchTaxonomyViewModel : BaseNopEntityModel
    {
        public string Name { get; set; }
        public int GoogleTaxonomyId { get; set; }
        public string parentTaxonomy { get; set; }
    }
}
