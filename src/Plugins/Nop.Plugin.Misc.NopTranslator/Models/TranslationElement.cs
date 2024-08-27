using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nop.Plugin.Misc.NopTranslator.Models
{
    public class TranslationElement
    {
        public string OpenTag { get; set; }
        public object Content { get; set; }
        public string CloseTag { get; set; }
        public bool IsTranslatable { get; set; }
    }

}
