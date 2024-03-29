using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core.Domain.Orders;
using Nop.Core;
using Nop.Plugin.Misc.NopTranslator.Models;
using Nop.Services.Catalog;
using Nop.Services.Security;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;
using Nop.Web.Framework;
using Newtonsoft.Json;
using System.Net.Http;
using System.Text;

namespace Nop.Plugin.Misc.NopTranslator.Controllers;
[AuthorizeAdmin]
[Area(AreaNames.Admin)]
[AutoValidateAntiforgeryToken]
public class NopTranslatorController : BasePluginController
{
    #region Fields
    private IProductService _productService;

    #endregion

    public NopTranslatorController(IProductService productService)
    {
        _productService=productService;
    }
   
    public async Task<TranslateResponse> Translate(TranslateRequest request)
    {
        var url = "http://localhost:3000/api/v1/translate";

        var requestBody = new
        {
            source =request.Source,
            target = request.Target,
            text = request.Text
        };

        var json = JsonConvert.SerializeObject(requestBody);

        var httpClient = new HttpClient();
        var response = await httpClient.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));

        var responseContent = await response.Content.ReadAsStringAsync();

        var translatedText = JsonConvert.DeserializeObject<TranslateResponse>(responseContent);


        return translatedText;
    }
    // GET
    public async Task<IActionResult> Configure()
    {
        

        var model = new ConfigurationModel
        {
            
        };

      

        return View("~/Plugins/Nop.Plugin.Misc.NopTranslator/Views/Configure.cshtml", model);
    }

    [HttpPost]
    public async Task<IActionResult> Configure(ConfigurationModel model)
    {

        var productsList = await _productService.SearchProductsAsync(
            categoryIds: null,
            pageSize: 100,
            showHidden: true
        );

        foreach (var product in productsList)
        {
            TranslateRequest request = new TranslateRequest();
            request.Source = "auto";
            request.Target = "tr";
            request.Text = product.FullDescription;
            var result=await Translate(request);
            var translatedFullDescription = result.translation;
        }
        return await Configure();
    }
}