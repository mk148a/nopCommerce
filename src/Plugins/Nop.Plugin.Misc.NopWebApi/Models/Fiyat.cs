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
    public class Fiyat : BaseEntity
    {
        public Fiyat()
        {
            // Fotograflar = new Fotograflar();
          //  OlusturmaTarihi = DateTime.Now;
        }

        public decimal YeniFiyat { get; set; }
        public decimal EskiFiyat { get; set; }
        public Currencies DovizCinsi { get; set; }
        public DateTime OlusturmaTarihi { get; set; }
        public DateTime GuncellemeTarihi { get; set; }

    }

    public enum Currencies : int
    {
        TRY = 0,
        USD = 1,
        EUR = 2,
        GBP = 3,
        AUD = 4
    }
}
