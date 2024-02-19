using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nop.Plugin.Misc.GoogleMultiLanguageAndCurrency.Models
{
    public class GoogleMultiLanguageAndCurrencysModel
    {
        public List<GoogleMultiLanguageAndCurrencyModel> LinkTags { get; set; } = new List<GoogleMultiLanguageAndCurrencyModel>();
    }
    public class GoogleMultiLanguageAndCurrencyModel
    {
        public string Rel { get; set; }
        public string Hreflang { get; set; }
        public string Href { get; set; }
    }
}
