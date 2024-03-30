
using System.Threading.Tasks;

namespace Nop.Plugin.Misc.NopTranslator.Services
{
    public partial interface ITranslateService
    {
      Task<string> TranslateProducts();


    }
}