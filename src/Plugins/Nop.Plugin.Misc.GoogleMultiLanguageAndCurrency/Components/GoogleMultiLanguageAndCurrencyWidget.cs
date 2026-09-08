using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Nop.Core;
using Nop.Core.Domain.Localization;
using Nop.Plugin.Misc.GoogleMultiLanguageAndCurrency.Models;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Seo;
using Microsoft.AspNetCore.Routing;
using Nop.Core.Domain.Seo;

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
        private List<Language> _activeLanguages;

        public GoogleMultiLanguageAndCurrencyWidget(
            IUrlRecordService urlRecordService,
            ILanguageService languageService,
            ISettingService settingService,
            IStoreContext storeContext,
            ILoggerFactory loggerFactory)
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
            string currentUrl = "";
            try
            {

                var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
                var localizationSettings = await _settingService.LoadSettingAsync<LocalizationSettings>(storeScope);
                if (!localizationSettings.SeoFriendlyUrlsForLanguagesEnabled)
                {
                    return View("~/Plugins/Nop.Plugin.Misc.GoogleMultiLanguageAndCurrency/Views/Shared/Components/GoogleMultiLanguageAndCurrencyWidget/Default.cshtml", model);
                }

                var data = Url.ActionContext.RouteData;
                if (data == null || data.Values.Count == 0)
                {
                    return View("~/Plugins/Nop.Plugin.Misc.GoogleMultiLanguageAndCurrency/Views/Shared/Components/GoogleMultiLanguageAndCurrencyWidget/Default.cshtml", model);
                }
                var currentLanguageTxt = data.Values["language"] as string;
                if (_activeLanguages == null)
                    _activeLanguages = (await _languageService.GetAllLanguagesAsync(false, storeScope)).Where(x => x.Published == true).ToList();
                if (string.IsNullOrEmpty(currentLanguageTxt) || _activeLanguages.All(x => x.UniqueSeoCode != currentLanguageTxt))
                {
                    return View("~/Plugins/Nop.Plugin.Misc.GoogleMultiLanguageAndCurrency/Views/Shared/Components/GoogleMultiLanguageAndCurrencyWidget/Default.cshtml", model);
                }
                var activeLanguage = _activeLanguages.Single(x => x.UniqueSeoCode == currentLanguageTxt);
                var currentLanguageId = activeLanguage.Id;
                var currentStore = await _storeContext.GetCurrentStoreAsync();
                var defaultLang = _activeLanguages.Single(z => z.Id == currentStore.DefaultLanguageId);

                model = await GenerateHreflangLinks(data, _activeLanguages, activeLanguage, currentStore, defaultLang, storeScope);

                currentUrl = await GetHttpProtocolAsync() + "://" + HttpContext.Request.Host.Value + HttpContext.Request.Path + HttpContext.Request.QueryString;


            }
            catch (Exception ex)
            {
                _logger.LogCritical(DateTime.Now + "-" + "GoogleMultiLanguageAndCurrency error:" + ex.Message + Environment.NewLine + ex.StackTrace);
            }
            finally
            {
                stopwatch.Stop();
                var elapsedTime = stopwatch.ElapsedMilliseconds;
                if (elapsedTime > 100)
                {
                    _logger.LogWarning("Google language widget took {ElapsedMilliseconds} ms for {Url}", elapsedTime, currentUrl);
                }
            }

            return View("~/Plugins/Nop.Plugin.Misc.GoogleMultiLanguageAndCurrency/Views/Shared/Components/GoogleMultiLanguageAndCurrencyWidget/Default.cshtml", model);
        }
        private async Task<GoogleMultiLanguageAndCurrencysModel> GenerateHreflangLinks(RouteData data, List<Language> activeLanguages, Language currentLanguage,
             Core.Domain.Stores.Store currentStore, Language defaultLang, int storeScope)
        {
            var model = new GoogleMultiLanguageAndCurrencysModel();
            var actionKeys = Url.ActionContext.ModelState.Keys.ToList();
            var controllerName = data.Values["controller"] as string;
            var pathBase = HttpContext.Request.PathBase;
            var currentUrl = await GetHttpProtocolAsync() + "://" + HttpContext.Request.Host.Value + HttpContext.Request.Path + HttpContext.Request.QueryString;
            var scheme = new Uri(currentUrl).GetComponents(UriComponents.SchemeAndServer,
                        UriFormat.Unescaped);
            var path = new Uri(currentUrl).PathAndQuery;
            UrlRecord urlRecord = null;
            var currentLanguageId = currentLanguage.Id;
            if (actionKeys.Count > 0 && actionKeys.Any(x => x.EndsWith("id") && data.Values[x] != null && int.TryParse(data.Values[x].ToString(), out _)))
            {
                var key = actionKeys.Single(x => !string.IsNullOrEmpty(x));
                int entitiyId = int.Parse(data.Values[key].ToString());
                var slug = data.Values["sename"];
                if (slug != null)
                {
                    urlRecord = await _urlRecordService.GetBySlugAsync(slug.ToString());
                }
                else
                {
                    foreach (var activeLanguagee in activeLanguages)
                    {
                        var activeSlug = await _urlRecordService.GetActiveSlugAsync(entitiyId, controllerName, activeLanguagee.Id);
                        if (!string.IsNullOrEmpty(activeSlug))
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
                if (slug != null)
                {
                    urlRecord = await _urlRecordService.GetBySlugAsync(slug.ToString());
                }
                else
                {
                    AddDefaultLanguageHreflang(model, pathBase, path, scheme, defaultLang);
                    foreach (var activeLanguagee in activeLanguages)
                    {
                        if (activeLanguagee.Id == currentLanguageId)
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
                            Rel = "alternate",
                            Hreflang = hreflang,
                            Href = localizedUrl
                        });
                    }
                }
            }
            if (urlRecord != null)
            {
                var hostName = await GetHttpProtocolAsync() + "://" + HttpContext.Request.Host.Value;
                AddSeoUrlDefaultLanguageHreflang(model, urlRecord, currentStore, hostName, defaultLang);
                foreach (var language in activeLanguages)
                {
                    if (language.Id == currentLanguageId)
                    {
                        continue;
                    }
                    var alternateUrl = await GetSeoUrlForLanguage(urlRecord, currentStore, language);


                    var hreflang = language.LanguageCulture;

                    model.LinkTags.Add(new GoogleMultiLanguageAndCurrencyModel
                    {
                        Rel = "alternate",
                        Hreflang = hreflang,
                        Href = $"{hostName}/{language.UniqueSeoCode}/{alternateUrl}"
                    });
                }
            }
            return model;
        }
        private async Task<string> GetSeoUrlForLanguage(UrlRecord urlRecord, Core.Domain.Stores.Store currentStore, Language language)
        {
            var alternateUrl = await _urlRecordService.GetActiveSlugAsync(urlRecord.EntityId,
                              urlRecord.EntityName, language.Id);
            if (string.IsNullOrEmpty(alternateUrl))
            {
                alternateUrl = await _urlRecordService.GetActiveSlugAsync(urlRecord.EntityId,
                      urlRecord.EntityName, currentStore.DefaultLanguageId);
                if (string.IsNullOrEmpty(alternateUrl))
                {
                    alternateUrl = await _urlRecordService.GetActiveSlugAsync(urlRecord.EntityId,
                                          urlRecord.EntityName, 0);
                    if (string.IsNullOrEmpty(alternateUrl))
                    {
                        alternateUrl = urlRecord.Slug;
                    }
                }
            }

            return alternateUrl;
        }
        private void AddSeoUrlDefaultLanguageHreflang(GoogleMultiLanguageAndCurrencysModel model, UrlRecord urlRecord, Core.Domain.Stores.Store currentStore, string hostName, Language defaultLang)
        {
            bool defaultLanguageHrefIsAdded = model.LinkTags.Any(x => x.Hreflang == "x-default");
            if (!defaultLanguageHrefIsAdded)
            {
                var alternateUrl = GetSeoUrlForLanguage(urlRecord, currentStore, defaultLang).Result;
                model.LinkTags.Add(new GoogleMultiLanguageAndCurrencyModel
                {
                    Rel = "alternate",
                    Hreflang = "x-default",
                    Href = $"{hostName}/{defaultLang.UniqueSeoCode}/{alternateUrl}"
                });
            }
        }
        private void AddDefaultLanguageHreflang(GoogleMultiLanguageAndCurrencysModel model, string pathBase, string path, string scheme, Language defaultLang)
        {
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
        }
    }
}
