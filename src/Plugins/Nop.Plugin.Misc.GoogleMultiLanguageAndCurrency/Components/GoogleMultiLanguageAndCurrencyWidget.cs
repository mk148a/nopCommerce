using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using DocumentFormat.OpenXml.InkML;
using DocumentFormat.OpenXml.Spreadsheet;
using LinqToDB.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Nop.Core;
using Nop.Core.Domain.Localization;
using Nop.Core.Domain.Security;
using Nop.Core.Domain.Seo;
using Nop.Core.Infrastructure;
using Nop.Plugin.Misc.GoogleMultiLanguageAndCurrency.Models;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Plugins;
using Nop.Services.Seo;
using Nop.Web.Framework.Mvc.Routing;
using ILogger = Nop.Services.Logging.ILogger;

namespace Nop.Plugin.Misc.GoogleMultiLanguageAndCurrency.Components
{
    [ViewComponent(Name = "GoogleMultiLanguageAndCurrencyWidget")]
    public class GoogleMultiLanguageAndCurrencyWidget : ViewComponent
    {
      
        private readonly IUrlRecordService _urlRecordService;
        private readonly ILanguageService _languageService;
        private readonly ISettingService _settingService;
        private readonly IStoreContext _storeContext;
        private readonly ILogger<GoogleMultiLanguageAndCurrencyWidget> _logger;

        public GoogleMultiLanguageAndCurrencyWidget( IUrlRecordService urlRecordService,
            ILanguageService languageService, IWebHelper webHelper, ISettingService settingService,IStoreContext storeContext
            , ILoggerFactory loggerFactory)
        {
            _urlRecordService = urlRecordService;
            _languageService = languageService;
            _settingService = settingService;
            _storeContext = storeContext;
            _logger = loggerFactory.CreateLogger<GoogleMultiLanguageAndCurrencyWidget>();


        }
        protected virtual async Task<string> GetHttpProtocolAsync()
        {
            var store = await _storeContext.GetCurrentStoreAsync();

            return store.SslEnabled ? Uri.UriSchemeHttps : Uri.UriSchemeHttp;
        }
        public async Task<IViewComponentResult> InvokeAsync()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var model = new GoogleMultiLanguageAndCurrencysModel();
            try
            {
                var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var localizationSettings = await _settingService.LoadSettingAsync<LocalizationSettings>(storeScope);

            if (localizationSettings.SeoFriendlyUrlsForLanguagesEnabled)
            {
                var data = Url.ActionContext.RouteData;
                if (data != null && data.Values.Count > 0)
                {
                   
                    var currentLanguageTxt = data.Values["language"] as string;
                    var activeLanguages = (await _languageService.GetAllLanguagesAsync(false, storeScope)).Where(x=>x.Published=true);
                    var activeLanguage = activeLanguages.Single(x => x.UniqueSeoCode == currentLanguageTxt);
                    var currentLanguageId = activeLanguage.Id;
                    var currentStore = await _storeContext.GetCurrentStoreAsync();
                    var defaultLang = activeLanguages.Single(z => z.Id == currentStore.DefaultLanguageId);

                    var actionKeys = Url.ActionContext.ModelState.Keys.ToList();
                    var actionDescriptor = Url.ActionContext.ActionDescriptor;
                    var actionName = data.Values["action"] as string;
                    var controllerName = data.Values["controller"] as string;

                    var pathBase = Url.ActionContext.HttpContext.Request.PathBase;
                    var currentUrl = await GetHttpProtocolAsync() + "://" + HttpContext.Request.Host.Value + HttpContext.Request.Path;
                    pathBase = HttpContext.Request.PathBase;

                    //Extract server and path from url
                    var scheme = new Uri(currentUrl).GetComponents(UriComponents.SchemeAndServer,
                        UriFormat.Unescaped);
                    var path = new Uri(currentUrl).LocalPath;

                    

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
                            //var urlHelper = _urlHelperFactory.GetUrlHelper(Url.ActionContext);

                            bool defaultLanguageHrefIsAdded = model.LinkTags.Any(x => x.Hreflang == "x-default");
                            if (!defaultLanguageHrefIsAdded)
                            {
                                //Replace seo code
                                var defaultLanglocalizedPath = path
                                    .RemoveLanguageSeoCodeFromUrl(pathBase, true)
                                    .AddLanguageSeoCodeToUrl(pathBase, true, defaultLang);

                                var defaultLangUrl = new Uri(new Uri(scheme), defaultLanglocalizedPath).ToString();


                                model.LinkTags.Add(new GoogleMultiLanguageAndCurrencyModel
                                {
                                    Rel = "alternate",
                                    Hreflang = "x-default",
                                    Href = defaultLangUrl
                                });
                            }

                            foreach (var activeLanguagee in activeLanguages)
                            {
                                if (activeLanguagee.Id== currentLanguageId)
                                {
                                  continue;
                                }
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

                    if (urlRecord != null) //SEO URL
                    {
                        var hostName = await GetHttpProtocolAsync() + "://" + HttpContext.Request.Host.Value;

                        bool defaultLanguageHrefIsAdded = model.LinkTags.Any(x => x.Hreflang == "x-default");
                        if (!defaultLanguageHrefIsAdded)
                        {

                            var alternateUrl = await _urlRecordService.GetActiveSlugAsync(urlRecord.EntityId,
                                urlRecord.EntityName, currentStore.DefaultLanguageId);
                            if (alternateUrl.IsNullOrEmpty())
                            {
                                alternateUrl = await _urlRecordService.GetActiveSlugAsync(urlRecord.EntityId,
                                    urlRecord.EntityName, 0);
                                if (alternateUrl.IsNullOrEmpty())
                                {
                                    alternateUrl = urlRecord.Slug;
                                }
                               
                            }

                          
                            model.LinkTags.Add(new GoogleMultiLanguageAndCurrencyModel
                            {
                                Rel = "alternate",
                                Hreflang = "x-default",
                                Href = $"{hostName}/{defaultLang.UniqueSeoCode}/{alternateUrl}"
                            });
                        }
                        foreach (var language in activeLanguages)
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
                                    if (alternateUrl.IsNullOrEmpty())
                                    {
                                        alternateUrl = await _urlRecordService.GetActiveSlugAsync(urlRecord.EntityId,
                                            urlRecord.EntityName, 0);
                                        if (alternateUrl.IsNullOrEmpty())

                                        {
                                            alternateUrl = urlRecord.Slug;
                                        }
                                    }
                                }
                               

                                var hreflang = language.LanguageCulture;

                                model.LinkTags.Add(new GoogleMultiLanguageAndCurrencyModel
                                {
                                    Rel = "alternate",
                                    Hreflang = hreflang,
                                    Href = $"{hostName}/{language.UniqueSeoCode}/{alternateUrl}"
                                });
                            }


                        }
                    }








                }
            }

            }
            finally
            {
                stopwatch.Stop();
                var elapsedTime = stopwatch.ElapsedMilliseconds;
                Console.WriteLine($"Gecikme: {elapsedTime} ms");
                if (elapsedTime > 100)
                {
                    // Gecikme 100 ms'den fazlaysa bir uyarı günlüğü kaydedin.
                    _logger.LogWarning($"Widget gecikti: {elapsedTime} ms");
                }
            }

            return View("~/Plugins/Nop.Plugin.Misc.GoogleMultiLanguageAndCurrency/Views/Shared/Components/GoogleMultiLanguageAndCurrencyWidget/Default.cshtml", model);
        }
    }
}
