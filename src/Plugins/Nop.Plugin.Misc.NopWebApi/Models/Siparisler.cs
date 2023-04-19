using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Wordprocessing;
using Nop.Core;

namespace Nop.Plugin.Misc.NopWebApi.Models
{
    public class Siparisler : BaseEntity
    {
      
        public DateTime Tarih { get; set; }

    
        public DateTime SiparisTarih { get; set; }
        
        public int Adet { get; set; }
      
        public int VaryasyonAdet { get; set; }

      
        public decimal BirimFiyat { get; set; }

        public string Sku { get; set; }
        public string ParaBirimi { get; set; }
        
        public string SiparisDurumu { get; set; }
        public int UyeId { get; set; }

        public long ReceiptId { get; set; }
        public long TransacationId { get; set; }
        public string SiparisNotu { get; set; } = "";
        

       
    }
}
