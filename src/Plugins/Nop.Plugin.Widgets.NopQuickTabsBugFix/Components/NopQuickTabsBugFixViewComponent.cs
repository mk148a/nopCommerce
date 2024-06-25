using System;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Nop.Plugin.Widgets.NopQuickTabsBugFix.Models;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Widgets.NopQuickTabsBugFix.Components
{
    [ViewComponent(Name = "NopQuickTabsBugFix")]
    public class NopQuickTabsBugFixViewComponent : NopViewComponent
    {
        public NopQuickTabsBugFixViewComponent()
        {

        }

        public IViewComponentResult Invoke(object model)
        {
            var ss=JsonConvert.SerializeObject(model);
            var dd = JsonConvert.DeserializeObject<ContactUsModel>(ss);
          
           




            return View("~/Plugins/Widgets.NopQuickTabsBugFix/Views/NopQuickTabsBugFix.cshtml", dd);
        }
    }
}
