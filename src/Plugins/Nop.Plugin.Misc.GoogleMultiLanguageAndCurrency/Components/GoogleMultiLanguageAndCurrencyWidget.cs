using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using DocumentFormat.OpenXml.InkML;
using LinqToDB.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
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
        private readonly IUrlHelperFactory _urlHelperFactory;


        public GoogleMultiLanguageAndCurrencyWidget(IWorkContext workContext, IUrlRecordService urlRecordService, ILocalizationService localizationService,
            ILanguageService languageService, IWebHelper webHelper, ISettingService settingService,IStoreContext storeContext,IUrlHelperFactory urlHelperFactory)
        {
            _workContext = workContext;
            _urlRecordService = urlRecordService;
            _localizationService = localizationService;
            _languageService = languageService;
            _webHelper = webHelper;
            _settingService = settingService;
            _storeContext = storeContext;
            _urlHelperFactory=urlHelperFactory;
      
            
        }
        protected virtual async Task<string> GetHttpProtocolAsync()
        {
            var store = await _storeContext.GetCurrentStoreAsync();

            return store.SslEnabled ? Uri.UriSchemeHttps : Uri.UriSchemeHttp;
        }
        public async Task<IViewComponentResult> InvokeAsync()
        {
            ///TOdo:test et
            var model = new GoogleMultiLanguageAndCurrencysModel();

            var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var localizationSettings = await _settingService.LoadSettingAsync<LocalizationSettings>(storeScope);

            if (localizationSettings.SeoFriendlyUrlsForLanguagesEnabled)
            {
                var data = Url.ActionContext.RouteData;
                if (data != null && data.Values.Count > 0)
                {

                    var currentLanguageTxt = data.Values["language"] as string;
                    var activeLanguages = await _languageService.GetAllLanguagesAsync(false, storeScope);
                    var activeLanguage = activeLanguages.Single(x => x.UniqueSeoCode == currentLanguageTxt);
                    var currentLanguageId = activeLanguage.Id;
                    var currentStore = await _storeContext.GetCurrentStoreAsync();


                    var actionKeys = Url.ActionContext.ModelState.Keys.ToList();
                    var actionDescriptor = Url.ActionContext.ActionDescriptor;
                    var actionName = data.Values["action"] as string;
                    var controllerName = data.Values["controller"] as string;

                    UrlRecord urlRecord= null;
                    if (actionKeys.Count > 0)
                    {
                        var key = actionKeys.Single(x=>!x.IsNullOrEmpty());

                        int entitiyId =int.Parse(data.Values[key].ToString());

                        var slug = data.Values["sename"];
                        if (slug != null)
                        {
                            urlRecord = await _urlRecordService.GetBySlugAsync((string)slug);
                        }
                        else
                        {
                            foreach (var activeLanguagee in activeLanguages)
                            {
                                var activeSlug = await _urlRecordService.GetActiveSlugAsync(entitiyId, controllerName, activeLanguagee.Id);
                                if (!activeSlug.IsNullOrEmpty())
                                {
                                    urlRecord = await _urlRecordService.GetBySlugAsync(activeSlug);
                                    break;
                                }

                            }
                        }
                       
                       
                    }
                    else
                    {
                       
                        var slug = data.Values["sename"];
                        if (slug!=null)
                        {
                            urlRecord = await _urlRecordService.GetBySlugAsync((string)slug);
                        }
                        else
                        {
                            var urlHelper = _urlHelperFactory.GetUrlHelper(Url.ActionContext);
                            var pathBase = Url.ActionContext.HttpContext.Request.PathBase;

                            foreach (var activeLanguagee in activeLanguages)
                            {
                                if (activeLanguagee.Id== currentLanguageId)
                                {
                                  continue;
                                }
                                var currentUrl = urlHelper.RouteUrl(actionName,
                                    await GetHttpProtocolAsync());
                                
                                if (currentUrl==null)
                                {
                                    currentUrl= Url.RouteUrl(actionName);
                                    if (currentUrl == null)
                                    {
                                        currentUrl = await GetHttpProtocolAsync()+"://"+ HttpContext.Request.Host.Value+ HttpContext.Request.Path;
                                        pathBase = HttpContext.Request.PathBase;


                                    }
                               
                                }
                                if (!string.IsNullOrEmpty(currentUrl))
                                {
                                   

                                //Extract server and path from url
                                var scheme = new Uri(currentUrl).GetComponents(UriComponents.SchemeAndServer,
                                    UriFormat.Unescaped);
                                var path = new Uri(currentUrl).PathAndQuery;

                                //Replace seo code
                                var localizedPath = path
                                    .RemoveLanguageSeoCodeFromUrl(pathBase, true)
                                    .AddLanguageSeoCodeToUrl(pathBase, true, activeLanguagee);

                                var localizedUrl = new Uri(new Uri(scheme), localizedPath).ToString();
                                var hreflang = activeLanguagee.LanguageCulture;

                                model.LinkTags.Add(new GoogleMultiLanguageAndCurrencyModel
                                {
                                    Rel = "alternate", Hreflang = hreflang, Href = localizedUrl
                                });
                            }
                              
                        }
                              
                        }


                    }


                    

                    



                  
                    foreach (var language in activeLanguages)
                    {
                        if (urlRecord!=null) //SEO URL
                        {
                           
                            if (language.Id != currentLanguageId)
                            {
                                //Todo:Burada sıçıyor bak
                                var alternateUrl = await _urlRecordService.GetActiveSlugAsync(urlRecord.EntityId,
                                    urlRecord.EntityName, language.Id);
                                if (alternateUrl.IsNullOrEmpty())
                                {
                                    alternateUrl = await _urlRecordService.GetActiveSlugAsync(urlRecord.EntityId,
                                        urlRecord.EntityName, currentStore.DefaultLanguageId);
                                }
                                var hreflang = language.LanguageCulture;

                                model.LinkTags.Add(new GoogleMultiLanguageAndCurrencyModel
                                {
                                    Rel = "alternate",
                                    Hreflang = hreflang,
                                    Href = $"{currentStore.Url}{language.UniqueSeoCode}/{alternateUrl}"
                                });
                            }
                        }

                    }
                    

                }
            }



            return View("~/Plugins/Nop.Plugin.Misc.GoogleMultiLanguageAndCurrency/Views/Shared/Components/GoogleMultiLanguageAndCurrencyWidget/Default.cshtml", model);
        }
    }
}
