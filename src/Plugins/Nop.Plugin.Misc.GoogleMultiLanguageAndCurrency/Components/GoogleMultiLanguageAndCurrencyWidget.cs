using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Seo;
using Nop.Plugin.Misc.GoogleMultiLanguageAndCurrency.Models;
using Nop.Services.Localization;
using Nop.Services.Seo;

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

        public GoogleMultiLanguageAndCurrencyWidget(IWorkContext workContext, IUrlRecordService urlRecordService, ILocalizationService localizationService,
            ILanguageService languageService, IWebHelper webHelper)
        {
            _workContext = workContext;
            _urlRecordService = urlRecordService;
            _localizationService = localizationService;
            _languageService = languageService;
            _webHelper = webHelper;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var model = new GoogleMultiLanguageAndCurrencysModel();

            // Ziyaret edilen sayfanın URL'sini al
            var currentUrl = _webHelper.GetThisPageUrl(true);
            // Ziyaret edilen sayfanın dilini belirle
            var currentLanguageId = (await _workContext.GetWorkingLanguageAsync()).Id;

            // Ziyaret edilen sayfanın URL'sini ve dilini içeren URL Record'ı al
            var urlRecords = await _urlRecordService.GetAllUrlRecordsAsync(currentUrl, currentLanguageId);

            if (urlRecords != null)
            {
                var urlRecord = urlRecords.First();
                if (urlRecord!=null)
                {
                    // Tüm dilleri al
                    var allLanguages = await _languageService.GetAllLanguagesAsync();

                    foreach (var language in allLanguages)
                    {
                        // Ziyaret edilen sayfanın dil haricindeki diller için link etiketleri oluştur
                        if (language.Id != currentLanguageId)
                        {
                            var alternateUrl = await _urlRecordService.GetActiveSlugAsync(urlRecord.EntityId,urlRecord.EntityName, language.Id);
                            var hreflang = language.UniqueSeoCode;

                            model.LinkTags.Add(new GoogleMultiLanguageAndCurrencyModel { Rel = "alternate", Hreflang = hreflang, Href = alternateUrl });
                        }
                    }
                }
               
            }

            return View("/Views/Shared/Components/GoogleMultiLanguageAndCurrencyWidget/Default.cshtml", model);
        }
    }
}
