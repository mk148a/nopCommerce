using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nop.Plugin.Misc.NopTranslator.Models
{
    public class TranslationProgress
    {
        public int PercentageComplete { get; set; }
        public string CurrentProduct { get; set; }
        public DateTime? StartTime { get; set; }
        public List<TranslationError> Errors { get; set; } = new List<TranslationError>(); // Hatalı ürünleri kaydetmek için
        public bool IsRunning { get; set; } // Yeni eklenen durum
    }
}


