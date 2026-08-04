using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Wordprocessing;
using FluentMigrator.Infrastructure;
using Nop.Core;

namespace Nop.Plugin.Misc.NopWebApi.Models
{
    public class Varyasyonlar 
    {
        public Varyasyonlar()
        {
            // Fotograflar = new Fotograflar();
        }
       public string VaryasyonTuru { get; set; }
       public string VaryasyonAdi { get; set; }
       public int? NopCommerceVaryasyonId { get; set; }
       public int? NopCommerceVaryasyonValueId { get; set; }
       public string Sku { get; set; }
       public Fiyat SatisFiyati{ get; set; }

        public int Adet { get; set; }
        public VaryasyonTipleri Tip { get; set; }
      
        public string FotografLinki { get; set; }

        public enum VaryasyonTipleri : int
        {
            Adet = 0,
            Renk = 1,
            Boyut = 2,
            Set = 3,
            Özellik = 4,
        }
    }
}
