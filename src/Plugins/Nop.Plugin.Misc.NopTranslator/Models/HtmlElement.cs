using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nop.Plugin.Misc.NopTranslator.Models
{

    public class HtmlElement
    {
        public string OpenTag { get; set; }
        public object Content { get; set; } // Bu içerik string veya başka bir HtmlContent listesi olabilir
        public string CloseTag { get; set; }
        public bool Translate { get; set; } // Çevrilip çevrilmeyeceğini belirler
    }
}
