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
    public class Siparisler 
    {
      
        public DateTime Tarih { get; set; }

    
        public DateTime SiparisTarih { get; set; }
        
        public int Adet { get; set; }
      
        public int VaryasyonAdet { get; set; }


        public Fiyat SatisFiyati { get; set; }

        public string Sku { get; set; }
       
        
        public string SiparisDurumu { get; set; }
       

        public long ReceiptId { get; set; }
        public long TransacationId { get; set; }
        public string SiparisNotu { get; set; } = "";
        public List<Varyasyonlar> VaryasyonList { get; set; }



    }
}
