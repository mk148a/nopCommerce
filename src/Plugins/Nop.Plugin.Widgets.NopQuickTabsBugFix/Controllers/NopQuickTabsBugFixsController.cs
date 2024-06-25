using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Messages;
using Nop.Core.Domain.Security;
using Nop.Core.Domain.Vendors;
using Nop.Core.Infrastructure;
using Nop.Core;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;
using Nop.Plugin.Widgets.NopQuickTabsBugFix.Models;
using Nop.Services.Localization;
using Nop.Core.Domain.Common;

namespace Nop.Plugin.Widgets.NopQuickTabsBugFix.Controllers
{

  
    [AutoValidateAntiforgeryToken]
    public class NopQuickTabsBugFixsController : BasePluginController
    {
        private readonly CaptchaSettings _captchaSettings;
        private readonly ILocalizationService _localizationService;
        private readonly CommonSettings _commonSettings;
        public  NopQuickTabsBugFixsController(CaptchaSettings captchaSettings, ILocalizationService localizationService, CommonSettings commonSettings )
        {
            _captchaSettings = captchaSettings;
            _localizationService = localizationService;
            _commonSettings = commonSettings;


        }


        [HttpPost]
        [ActionName("ProductContactUsAddNew")]
        [ValidateCaptcha("captchaValid")]
        public async Task<ActionResult> ProductContactUsAddNew(int id, object model, bool captchaValid)
        {
            var mdl=JsonConvert.SerializeObject(model);
            var mdlJ = JsonConvert.DeserializeObject<ContactUsModel>(mdl);
            if (_captchaSettings.Enabled && _captchaSettings.ShowOnContactUsPage && !captchaValid)
            {
              
                mdlJ.SuccessfullySent = false;
                ContactUsModel contactUsModel = mdlJ;
                contactUsModel.Result = await _localizationService.GetResourceAsync("Common.WrongCaptchaMessage");
                contactUsModel.Enquiry = string.Empty;
                contactUsModel.SubjectEnabled = _commonSettings.SubjectFieldOnContactUsForm;
                contactUsModel.DisplayCaptcha = _captchaSettings.Enabled && _captchaSettings.ShowOnContactUsPage;
                
               return ViewComponent("ProductContactUsTab", (object?)contactUsModel);
            }

            if (((ControllerBase)this).ModelState.IsValid)
            {
              
                return ((ControllerBase)(object)this).Content(mdlJ.Result);
            }

            return ((ControllerBase)(object)this).Content(mdlJ.Result);
        }
    }


}
