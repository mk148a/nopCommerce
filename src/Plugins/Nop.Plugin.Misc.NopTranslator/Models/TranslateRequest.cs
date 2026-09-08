using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nop.Plugin.Misc.NopTranslator.Models
{
    public class TranslateRequest
    {
        public string Source { get; set; }
        public string Target { get; set; }
        public string Text { get; set; }
        public string ProductName { get; set; }
        public int ProductId { get; set; }
    }
}
