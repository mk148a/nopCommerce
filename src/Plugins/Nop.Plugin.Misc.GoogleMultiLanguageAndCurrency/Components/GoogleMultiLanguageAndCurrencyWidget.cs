using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using DocumentFormat.OpenXml.InkML;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Nop.Core;
using Nop.Core.Domain.Localization;
using Nop.Core.Domain.Security;
using Nop.Core.Domain.Seo;
using Nop.Core.Infrastructure;
using Nop.Plugin.Misc.GoogleMultiLanguageAndCurrency.Models;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Seo;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Misc.GoogleMultiLanguageAndCurrency.Components
{
    [ViewComponent(Name = "GoogleMultiLanguageAndCurrencyWidget")]
    public class GoogleMultiLanguageAndCurrencyWidget : ViewComponent
    {
        private readonly IWorkContext _workContext;
        private readonly IWebHelper _webHelper;
        private readonly IUrlRecordService _urlRecordService;
        private readonly ILocalizationService _localizationService;
        private readonly ILanguageService _languageService;
        private readonly ISettingService _settingService;
        private readonly IStoreContext _storeContext;


       public GoogleMultiLanguageAndCurrencyWidget(IWorkContext workContext, IUrlRecordService urlRecordService, ILocalizationService localizationService,
            ILanguageService languageService, IWebHelper webHelper, ISettingService settingService,IStoreContext storeContext)
        {
            _workContext = workContext;
            _urlRecordService = urlRecordService;
            _localizationService = localizationService;
            _languageService = languageService;
            _webHelper = webHelper;
            _settingService = settingService;
            _storeContext = storeContext;
      
            
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            ///TOdo:test et
            var model = new GoogleMultiLanguageAndCurrencysModel();
            var data = Url.ActionContext.RouteData;
            if (data != null&&data.Values.Count>0)
            {
                var slug = data.Values["generic_se_name"] as string;
                var urlRecord = await _urlRecordService.GetBySlugAsync(slug);
                string pageURL = "";
            
                
         

                //base URL
                if (urlRecord != null)
                {
                    pageURL =await _urlRecordService.GetActiveSlugAsync(urlRecord.EntityId, urlRecord.EntityName, 0);
                }
                var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();

                var localizationSettings =   await _settingService.LoadSettingAsync<LocalizationSettings>(storeScope);

                var currentLanguageId = (await _workContext.GetWorkingLanguageAsync()).Id;
                //SEO Lang Url
                if (localizationSettings.SeoFriendlyUrlsForLanguagesEnabled)
                {
                    foreach (var language in (await _languageService.GetAllLanguagesAsync(storeId: storeScope)))
                    {
                        if (urlRecord != null) //SEO URL
                        {
                            pageURL =await _urlRecordService.GetSeNameAsync(urlRecord.EntityId, urlRecord.EntityName, language.Id);
                            if (language.Id != currentLanguageId)
                            {
                                var alternateUrl = await _urlRecordService.GetActiveSlugAsync(urlRecord.EntityId, urlRecord.EntityName, language.Id);
                                var hreflang = language.UniqueSeoCode;

                                model.LinkTags.Add(new GoogleMultiLanguageAndCurrencyModel { Rel = "alternate", Hreflang = hreflang, Href = alternateUrl });
                            }
                        }
                      
                    }
                }
              
            } 
        


            return View("/Views/Shared/Components/GoogleMultiLanguageAndCurrencyWidget/Default.cshtml", model);
        }
    }
}
