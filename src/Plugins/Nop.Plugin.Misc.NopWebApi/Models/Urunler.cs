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
    public class Urunler : BaseEntity
    {
        public Urunler()
        {
            // Fotograflar = new Fotograflar();
        }


        public string UrunAdi { get; set; }
        public string Sku { get; set; }
        public Fiyat SatisFiyati { get; set; }

        public string FotografLink { get; set; }


    }
}
