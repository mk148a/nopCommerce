using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nop.Core;

namespace Nop.Plugin.Misc.GoogleShoppingMultiCountry.Domains
{
    public partial class GoogleTaxonomyRecord : BaseEntity
    {
        public string Name { get; set; }
        public int ParentId { get; set; }
        public int GoogleTaxonomyId { get; set; }
        public virtual ICollection<GoogleTaxonomyRecord> SubCategories { get; set; }

    }
}
