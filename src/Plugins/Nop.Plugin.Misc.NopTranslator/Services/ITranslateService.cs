
using System.Collections.Generic;
using System.Threading.Tasks;
using Nop.Plugin.Misc.NopTranslator.Models;

namespace Nop.Plugin.Misc.NopTranslator.Services
{
    public partial interface ITranslateService
    {
        Task<List<TranslationResult>> TranslateProducts();
        Task<string> RetryTranslateProducts(List<int> productIds);
    }
}