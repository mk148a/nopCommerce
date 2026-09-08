using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nop.Core.Configuration;

namespace Nop.Plugin.Misc.NopTranslator
{
  
    public class NopTranslatorSettings : ISettings
    {

        public string data { get; set; }
        public bool license { get; set; }
        public string WidgetZone { get; set; }
        public int MaximumFile { get; set; }
        public int MaximumSize { get; set; }
    }
}
