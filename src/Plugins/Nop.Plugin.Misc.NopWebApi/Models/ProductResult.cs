using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nop.Core;

namespace Nop.Plugin.Misc.NopWebApi.Models
{
    public class ProductResult: BaseEntity
    {
        public ProductResult()
        {
            // Fotograflar = new Fotograflar();
        }
        public IList<Urunler> urunlerList { get; set; }
        public IList<Varyasyonlar> VaryasyonlarList { get; set; }
        public bool IsError { get; set; }


    }
}
