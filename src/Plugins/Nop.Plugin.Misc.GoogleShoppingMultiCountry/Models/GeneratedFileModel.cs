using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nop.Web.Framework.Models;

namespace Nop.Plugin.Misc.GoogleShoppingMultiCountry.Models
{
    public record GeneratedFileModel : BaseNopModel
    {
        public string StoreName { get; set; }
        public string FileUrl { get; set; }
        public string Language { get; set; }
    }
}
