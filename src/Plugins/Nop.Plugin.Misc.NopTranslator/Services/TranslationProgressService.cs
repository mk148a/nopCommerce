using System;
using System.Collections.Generic;
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
            _progress.IsRunning = true; // Çeviri işlemi başlatıldı
        }

        public void LogError(int productId, string productName, string errorMessage)
        {
            _progress.Errors.Add(new TranslationError
            {
                ProductId = productId,
                ProductName = productName,
                ErrorMessage = errorMessage
            });
        }

        public void StopProgress()
        {
            _progress.IsRunning = false; // Çeviri işlemi durduruldu veya tamamlandı
        }

        public void ClearProgress()
        {
            _progress = new TranslationProgress();
        }
    }
}