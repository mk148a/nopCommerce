using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nop.Core;

namespace Nop.Plugin.Misc.NopWebApi.Models
{
    public class OrderResult : BaseEntity
    {
        public OrderResult()
        {
            // Fotograflar = new Fotograflar();
        }
        public IList<Siparisler> SiparisList { get; set; }
        public bool IsError { get; set; }


    }
}
