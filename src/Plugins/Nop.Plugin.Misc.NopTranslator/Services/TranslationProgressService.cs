using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nop.Plugin.Misc.NopTranslator.Models;

namespace Nop.Plugin.Misc.NopTranslator.Services
{
    public class TranslationProgressService : ITranslationProgressService
    {
        private TranslationProgress _progress = new TranslationProgress();

        public TranslationProgress GetProgress()
        {
            return _progress;
        }

        public void UpdateProgress(int percentage, string currentProduct)
        {
            _progress.PercentageComplete = percentage;
            _progress.CurrentProduct = currentProduct;
        }

        public void UpdateStartTime(DateTime startTime)
        {
            _progress.StartTime = startTime;
        }
    }
}
