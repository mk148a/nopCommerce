using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nop.Plugin.Misc.NopTranslator.Models;

namespace Nop.Plugin.Misc.NopTranslator.Services
{
    public interface ITranslationProgressService
    {
        TranslationProgress GetProgress();
        void UpdateProgress(int percentage, string currentProduct);
        void UpdateStartTime(DateTime startTime);  // Yeni metod
        void ClearProgress();
        void LogError(int productId, string productName, string errorMessage);
        void StopProgress();


    }
}
