using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Policy;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.Drawing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Nop.Core;
using Nop.Core.Domain.Cms;
using Nop.Core.Infrastructure;
using Nop.Plugin.Widgets.CustomCustomProductReviews.Services;
using Nop.Plugin.Widgets.CustomProductReviews.Components;
using Nop.Services.Cms;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Plugins;
using Nop.Services.Stores;
using Nop.Web.Framework.Infrastructure;

namespace Nop.Plugin.Widgets.CustomProductReviews
{
    /// <summary>
    /// Rename this file and change to the correct type
    /// </summary>
    public class CustomProductReviewsPlugin : BasePlugin,IWidgetPlugin
    {


        #region Fields

        private readonly CustomProductReviewsSettings _customProdutReviewSettings;
        private readonly IActionContextAccessor _actionContextAccessor;
        private readonly ILocalizationService _localizationService;
        private readonly ILanguageService _languageService;
        private readonly ISettingService _settingService;
        private readonly IStoreService _storeService;
        private readonly IStoreContext _storeContext;
        private readonly IUrlHelperFactory _urlHelperFactory;
        private readonly IWebHelper _webHelper;
        private IPluginsInfo _pluginsInfo;
        private readonly IPluginService _pluginService;
        private readonly IHttpContextAccessor _httpContextAccessor;



        #endregion

        #region Ctor

        public CustomProductReviewsPlugin(CustomProductReviewsSettings customProdutReviewSettings,
            IActionContextAccessor actionContextAccessor,
            ILocalizationService localizationService,
            ILanguageService languageService,
            ISettingService settingService,
            IStoreService storeService,
            IUrlHelperFactory urlHelperFactory, IStoreContext storeContext, IWebHelper webHelper,IPluginService pluginService,IHttpContextAccessor httpContextAccessor)
        {
            _customProdutReviewSettings = customProdutReviewSettings;
            _actionContextAccessor = actionContextAccessor;
            _localizationService = localizationService;
            _languageService = languageService;
            _settingService = settingService;
            _storeService = storeService;
            _urlHelperFactory = urlHelperFactory;
            _storeContext = storeContext;
            _webHelper=webHelper;
            _pluginService = pluginService;
            _httpContextAccessor = httpContextAccessor;




        }

        #endregion

        #region Methods

        private static readonly IReadOnlyDictionary<string, string> ProductReviewsForTranslations =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = "Product reviews for",
                ["tr"] = "Ürün yorumları:",
                ["de"] = "Produktbewertungen für",
                ["fr"] = "Avis clients pour",
                ["es"] = "Opiniones de productos para",
                ["it"] = "Recensioni del prodotto per",
                ["pt"] = "Avaliações do produto para",
                ["nl"] = "Productbeoordelingen voor",
                ["da"] = "Produktanmeldelser for",
                ["hu"] = "Termékértékelések ehhez:",
                ["no"] = "Produktanmeldelser for",
                ["nn"] = "Produktanmeldingar for",
                ["pl"] = "Opinie o produkcie:",
                ["ro"] = "Recenzii pentru produsul",
                ["sv"] = "Produktrecensioner för",
                ["el"] = "Κριτικές προϊόντος για",
                ["ms"] = "Ulasan produk untuk",
                ["ru"] = "Отзывы о товаре:",
                ["uk"] = "Відгуки про товар:",
                ["ar"] = "مراجعات المنتج لـ",
                ["ur"] = "مصنوعات کے جائزے برائے",
                ["ja"] = "商品のレビュー：",
                ["zh"] = "产品评论：",
                ["ko"] = "제품 리뷰:"
            };

        private async Task EnsureProductReviewsForResourcesAsync()
        {
            var languages = await _languageService.GetAllLanguagesAsync(true);
            foreach (var language in languages)
            {
                var languageCode = language.LanguageCulture?.Split('-', StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault();
                if (string.IsNullOrWhiteSpace(languageCode))
                    languageCode = "en";

                var value = ProductReviewsForTranslations.TryGetValue(languageCode, out var translation)
                    ? translation
                    : ProductReviewsForTranslations["en"];

                await _localizationService.AddOrUpdateLocaleResourceAsync(
                    new Dictionary<string, string> { ["Reviews.ProductReviewsFor"] = value }, language.Id);

                var resources = GetReviewMediaResources(languageCode);
                await _localizationService.AddOrUpdateLocaleResourceAsync(resources, language.Id);
            }
        }

        private static Dictionary<string, string> GetReviewMediaResources(string languageCode)
        {
            // Keep the form useful even when a new storefront language is added:
            // the English copy is a safe fallback until a native translation is added.
            var values = languageCode?.ToLowerInvariant() switch
            {
                "tr" => ("Fotoğraf veya kısa video ekleyin", "Deneyiminizi gösteren fotoğraf ya da kısa video yorumunuzu daha faydalı kılar.", "En fazla {0} dosya. Fotoğraf başına {1} MB, kısa video başına {2} MB. JPG, PNG, WebP, MP4, MOV veya WebM.", "Seçilen dosyalar"),
                "de" => ("Foto oder kurzes Video hinzufügen", "Fotos oder ein kurzes Video machen Ihre Bewertung hilfreicher.", "Bis zu {0} Dateien. Fotos bis {1} MB, kurze Videos bis {2} MB. JPG, PNG, WebP, MP4, MOV oder WebM.", "Ausgewählte Dateien"),
                "fr" => ("Ajoutez des photos ou une courte vidéo", "Des photos ou une courte vidéo rendent votre avis plus utile.", "Jusqu’à {0} fichiers. Photos jusqu’à {1} Mo, courtes vidéos jusqu’à {2} Mo. JPG, PNG, WebP, MP4, MOV ou WebM.", "Fichiers sélectionnés"),
                "es" => ("Añade fotos o un vídeo corto", "Las fotos o un vídeo corto hacen que tu reseña sea más útil.", "Hasta {0} archivos. Fotos de hasta {1} MB y vídeos cortos de hasta {2} MB. JPG, PNG, WebP, MP4, MOV o WebM.", "Archivos seleccionados"),
                "it" => ("Aggiungi foto o un breve video", "Le foto o un breve video rendono la recensione più utile.", "Fino a {0} file. Foto fino a {1} MB e brevi video fino a {2} MB. JPG, PNG, WebP, MP4, MOV o WebM.", "File selezionati"),
                "pt" => ("Adicione fotos ou um vídeo curto", "Fotos ou um vídeo curto tornam a sua avaliação mais útil.", "Até {0} ficheiros. Fotografias até {1} MB e vídeos curtos até {2} MB. JPG, PNG, WebP, MP4, MOV ou WebM.", "Ficheiros selecionados"),
                "nl" => ("Voeg foto’s of een korte video toe", "Foto’s of een korte video maken uw beoordeling nuttiger.", "Maximaal {0} bestanden. Foto’s tot {1} MB en korte video’s tot {2} MB. JPG, PNG, WebP, MP4, MOV of WebM.", "Geselecteerde bestanden"),
                "ru" => ("Добавьте фотографии или короткое видео", "Фотографии или короткое видео сделают ваш отзыв полезнее.", "До {0} файлов. Фотографии до {1} МБ, короткие видео до {2} МБ. JPG, PNG, WebP, MP4, MOV или WebM.", "Выбранные файлы"),
                _ => ("Add photos or a short video", "Photos or a short video make your review more helpful for other archers.", "Up to {0} files. Photos up to {1} MB each; short videos up to {2} MB. JPG, PNG, WebP, MP4, MOV or WebM.", "Selected files")
            };

            return new Dictionary<string, string>
            {
                ["Plugins.Widgets.CustomProductReviews.Media.Heading"] = values.Item1,
                ["Plugins.Widgets.CustomProductReviews.Media.Guidance"] = values.Item2,
                ["Plugins.Widgets.CustomProductReviews.Media.Requirements"] = values.Item3,
                ["Plugins.Widgets.CustomProductReviews.Media.SelectedFiles"] = values.Item4,
                ["Plugins.Widgets.CustomProductReviews.Media.InvalidType"] = "Unsupported file type.",
                ["Plugins.Widgets.CustomProductReviews.Media.TooMany"] = "Too many files selected.",
                ["Plugins.Widgets.CustomProductReviews.Media.TooLarge"] = "A selected file is too large."
            };
        }

        /// <summary>
        /// Gets a configuration page URL
        /// </summary>
        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation().TrimEnd('/')}/Admin/ReviewMedia/Manage";
        }

        /// <summary>
        /// Gets widget zones where this widget should be rendered
        /// </summary>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the widget zones
        /// </returns>
        public async Task<IList<string>> GetWidgetZonesAsync()
        {
            // Render the custom list only before the core review partial. The
            // previous second zone rendered every review a second time and
            // made visible counts diverge from the summary/schema.
            return await Task.FromResult<IList<string>>(
                new List<string>
                {
                    PublicWidgetZones.ProductReviewsPageTop,
                    AdminWidgetZones.ProductReviewDetailsTop,
                    AdminWidgetZones.ProductReviewListButtons
                });
           
           
        }

        /// <summary>
        /// Gets a name of a view component for displaying widget
        /// </summary>
        /// <param name="widgetZone">Name of the widget zone</param>
        /// <returns>View component name</returns>
        public Type GetWidgetViewComponent(string widgetZone)
        {
            if (widgetZone == null)
                throw new ArgumentNullException(nameof(widgetZone));

            return string.Equals(widgetZone, AdminWidgetZones.ProductReviewDetailsTop, StringComparison.Ordinal) ||
                   string.Equals(widgetZone, AdminWidgetZones.ProductReviewListButtons, StringComparison.Ordinal)
                ? typeof(AdminReviewMediaWidgetViewComponent)
                : typeof(CustomProductReviewsViewComponent);
        }


        /// <summary>
        /// Install plugin
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
        public override async Task InstallAsync()
        {

            try
            {
                //    _pluginsInfo = Singleton<IPluginsInfo>.Instance;


                //    var client = new HttpClient();
                //client.DefaultRequestHeaders.Accept.Clear();
                //client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                //string rq = _httpContextAccessor.HttpContext.Request.PathBase;


                //Uri myUri = new Uri(rq);

                //string host = myUri.Host;


                //host = EncryptService.Encrypt(host);
                //var version = "1.0.0";
                //version = "CustomProductReviews" + " " + version;
                //version = EncryptService.Encrypt(version);
                //string json = host + "," + version;
                //json = JsonConvert.SerializeObject(json);
                //var content = new StringContent(json, Encoding.UTF8, "application/json");
                //var result = await client.PostAsync("https://wupdater.duckdns.org/CustomerData", content);
                //var jsonString = await result.Content.ReadAsStringAsync();
                //bool rool = JsonConvert.DeserializeObject<bool>(jsonString);


                await _settingService.SaveSettingAsync(new CustomProductReviewsSettings
                {
                    WidgetZone = PublicWidgetZones.ProductReviewsPageTop,
                    data = "json",
                    MaximumFile = 5,
                    MaximumSize = 1073741824,
                    MaximumVideoSizeBytes = 104857600,
                    EnableReviewVideoTranscoding = false,
                    MaximumVideoDurationSeconds = 120,
                    MaximumVideoWidth = 3840,
                    MaximumVideoHeight = 3840,
                    NormalizedVideoMaxWidth = 1280,
                    VideoCrf = 23,
                    VideoTranscodeTimeoutSeconds = 180
                });

                await EnsureProductReviewsForResourcesAsync();




                await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
                {
                    ["Plugins.Widgets.CustomProductReviews.Fields.Enabled"] = "Enable",
                    ["Plugins.Widgets.CustomProductReviews.Fields.Enabled.Hint"] = "Check to activate this widget.",
                    ["Plugins.Widgets.CustomProductReviews.Fields.Script"] = "Installation script",
                    ["Plugins.Widgets.CustomProductReviews.Fields.Script.Hint"] =
                        "Find your unique installation script on the Installation tab in your account and then copy it into this field.",
                    ["Plugins.Widgets.CustomProductReviews.Fields.Script.Required"] =
                        "Installation script is required",
                });

                await base.InstallAsync();
            //    if (rool)
            //{

            //    //settings
            //    await _settingService.SaveSettingAsync(new CustomProductReviewsSettings
            //    {
            //        WidgetZone = PublicWidgetZones.ProductReviewsPageTop,
            //        data = json,
            //        MaximumFile = 5,
            //        MaximumSize = 1073741824
            //    });




            //    await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
            //    {
            //        ["Plugins.Widgets.CustomProductReviews.Fields.Enabled"] = "Enable",
            //        ["Plugins.Widgets.CustomProductReviews.Fields.Enabled.Hint"] = "Check to activate this widget.",
            //        ["Plugins.Widgets.CustomProductReviews.Fields.Script"] = "Installation script",
            //        ["Plugins.Widgets.CustomProductReviews.Fields.Script.Hint"] =
            //            "Find your unique installation script on the Installation tab in your account and then copy it into this field.",
            //        ["Plugins.Widgets.CustomProductReviews.Fields.Script.Required"] =
            //            "Installation script is required",
            //    });

            //    await base.InstallAsync();
            //}
            //else
            //{
            //    try
            //    {
            //        //var pluginToInstall = _pluginsInfo.PluginNamesToInstall.FirstOrDefault(plugin => plugin.SystemName.Equals("Nop.Plugin.Widgets.CustomProductReviews"));
            //        //_pluginsInfo.PluginNamesToInstall.Remove(pluginToInstall);
            //        //_pluginsInfo.PluginNamesToUninstall.Add("Nop.Plugin.Widgets.CustomProductReviews");
            //        //await _pluginsInfo.SaveAsync();
            //        _pluginService.ResetChanges();
            //        await _pluginService.PreparePluginToUninstallAsync("Nop.Plugin.Widgets.CustomProductReviews");
            //        await _pluginService.UninstallPluginsAsync();

            //        //_webHelper.RestartAppDomain();
            //    }
            //    catch 
            //    {
                   
            //    }
            //        // _pluginsInfo.PluginNamesToUninstall.Add("Nop.Plugin.Widgets.CustomProductReviews");

            //}
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
               
            }
        }

        public override async Task UpdateAsync(string currentVersion, string targetVersion)
        {
            await EnsureProductReviewsForResourcesAsync();
            await base.UpdateAsync(currentVersion, targetVersion);
        }

        /// <summary>
        /// Uninstall plugin
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
        public override async Task UninstallAsync()
        {
            await _settingService.DeleteSettingAsync<CustomProductReviewsSettings>();

            var stores = await _storeService.GetAllStoresAsync();
            var storeIds = new List<int> { 0 }.Union(stores.Select(store => store.Id));
            foreach (var storeId in storeIds)
            {
                var widgetSettings = await _settingService.LoadSettingAsync<WidgetSettings>(storeId);
                widgetSettings.ActiveWidgetSystemNames.Remove(CustomProductReviewsDefaults.SystemName);
                await _settingService.SaveSettingAsync(widgetSettings);
            }

            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Widgets.CustomProductReviews");

            await base.UninstallAsync();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets a value indicating whether to hide this plugin on the widget list page in the admin area
        /// </summary>
        public bool HideInWidgetList => false;

        #endregion


    }
}
