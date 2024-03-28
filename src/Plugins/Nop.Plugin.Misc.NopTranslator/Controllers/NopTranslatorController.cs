using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core.Domain.Orders;
using Nop.Core;
using Nop.Plugin.Misc.NopTranslator.Models;
using Nop.Services.Security;
using Nop.Web.Framework.Controllers;

namespace Nop.Plugin.Misc.NopTranslator.Controllers;

public class NopTranslatorController : Controller
{
    // GET
    public async Task<IActionResult> Configure()
    {
        

        var model = new ConfigurationModel
        {
            
        };

      

        return View("~/Plugins/Nop.Plugin.Misc.NopTranslator/Views/Configure.cshtml", model);
    }

    [HttpPost, ActionName("Configure")]
    [FormValueRequired("save")]
    public async Task<IActionResult> Configure(ConfigurationModel model)
    {
       

        return await Configure();
    }
}