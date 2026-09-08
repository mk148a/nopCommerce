using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nop.Core;

namespace Nop.Plugin.Misc.GoogleShoppingMultiCountry.Domains
{
    public partial class CategoryGoogleTaxonomyRecordMapping : BaseEntity
    {
        public int GoogleTaxonomyRecordId { get; set; }
        public int CategoryId { get; set; }
       

    }
}
