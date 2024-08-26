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
using System.Collections.Generic;

namespace Nop.Plugin.Misc.NopTranslator.Controllers;

[AuthorizeAdmin]
[Area(AreaNames.Admin)]
[AutoValidateAntiforgeryToken]
public class NopTranslatorController : BasePluginController
{
    #region Fields

    
    private IServiceScopeFactory _serviceScopeFactory;
    private readonly ITranslationProgressService _translationProgressService;

    #endregion

    public NopTranslatorController(
        IServiceScopeFactory serviceScopeFactory, ITranslationProgressService translationProgressService)
    {
        
        _serviceScopeFactory = serviceScopeFactory;
        _translationProgressService = translationProgressService;

    }

    

    // GET
    public async Task<IActionResult> Configure()
    {


        var model = new ConfigurationModel { };


        return View("~/Plugins/Nop.Plugin.Misc.NopTranslator/Views/Configure.cshtml", model);
    }


    [HttpPost]
    public IActionResult RetryTranslation([FromBody] List<int> productIds)
    {
        // Arka planda çalışacak işlem
        var process = new Thread(delegate ()
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var translationService = scope.ServiceProvider.GetService<ITranslateService>();
                var result = translationService.RetryTranslateProducts(productIds).Result;
                Console.WriteLine(result); // İşlemin sonucunu konsola yazdır
            }
        });

        process.Start(); // İşlemi başlat
        return Ok("Translation process started."); // Kullanıcıya işlem başladığını bildir
    }
    [HttpPost]
    public async Task<IActionResult> Configure(ConfigurationModel model)
    {
        using (var scope = _serviceScopeFactory.CreateScope())
        {
            var reportingService = scope.ServiceProvider.GetService<ITranslateService>();
            var progressService = scope.ServiceProvider.GetService<ITranslationProgressService>();

            var process = new Thread(delegate ()
            {
                try
                {
                    var result = reportingService.TranslateProducts().Result;
                    // Sonuçları ViewBag'de tutmuyoruz çünkü Thread içinde çalışıyoruz.
                }
                finally
                {
                    progressService.StopProgress(); // İşlem tamamlandığında durdur
                }
            });

            process.Start();
        }
        return await Configure();
    }

    [HttpGet]
    public JsonResult GetTranslationProgress()
    {
        var progress = _translationProgressService.GetProgress();
        return Json(progress);
    }
}