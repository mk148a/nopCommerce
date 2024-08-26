using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nop.Plugin.Misc.NopTranslator.Models
{
    public class TranslationResult
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public List<string> TranslatedLanguages { get; set; } = new List<string>();
    }
}
