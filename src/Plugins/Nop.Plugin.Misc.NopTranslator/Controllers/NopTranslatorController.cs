using System;
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
using K4os.Compression.LZ4.Engine;
using Nop.Core.Domain.Catalog;
using Nop.Services.Localization;
using Nop.Services.Seo;
using Nop.Web.Areas.Admin.Factories;
using Nop.Web.Areas.Admin.Models.Catalog;
using System.Linq;
using LinqToDB.Common;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Nop.Plugin.Misc.NopTranslator.Services;

namespace Nop.Plugin.Misc.NopTranslator.Controllers;

[AuthorizeAdmin]
[Area(AreaNames.Admin)]
[AutoValidateAntiforgeryToken]
public class NopTranslatorController : BasePluginController
{
    #region Fields

    
    private IServiceScopeFactory _serviceScopeFactory;
  

    #endregion

    public NopTranslatorController(
        IServiceScopeFactory serviceScopeFactory)
    {
        
        _serviceScopeFactory = serviceScopeFactory;

    }

    

    // GET
    public async Task<IActionResult> Configure()
    {


        var model = new ConfigurationModel { };


        return View("~/Plugins/Nop.Plugin.Misc.NopTranslator/Views/Configure.cshtml", model);
    }

   

    [HttpPost]
    public async Task<IActionResult> Configure(ConfigurationModel model)
    {
        //Get current store and active languages
        var process = new Thread(delegate ()
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var reportingService = scope.ServiceProvider.GetService<ITranslateService>();
                var result = reportingService.TranslateProducts();
                Console.WriteLine(result.Result);
            }
        });
        process.Start();
        return await Configure();
    }
}