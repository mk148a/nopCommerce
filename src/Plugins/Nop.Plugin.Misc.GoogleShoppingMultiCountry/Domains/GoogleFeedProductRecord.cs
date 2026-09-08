using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nop.Core;

namespace Nop.Plugin.Misc.GoogleShoppingMultiCountry.Domains
{
    public partial class GoogleFeedProductRecord:BaseEntity
    {
        public int ProductId { get; set; }
        public string Taxonomy { get; set; }

        public bool CustomGoods { get; set; }
        public string Gender { get; set; }
        public string AgeGroup { get; set; }
        public string Color { get; set; }
        public string Size { get; set; }
        public string Material { get; set; }
        public string Pattern { get; set; }
        public int LanguageId { get; set; }
    }
}
