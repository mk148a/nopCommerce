using System;
using System.Collections.Generic;
using Nop.Core.Http;
using Nop.Core;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Nop.Services.Cms;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Plugins;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace Nop.Plugin.Misc.EtsyToNopcommerce
{
    /// <summary>
    /// Rename this file and change to the correct type
    /// </summary>
    public class EtsyToNopcommercePlugin : BasePlugin
    {
        #region Fields

        private readonly EtsyToNopcommerceSettings _etsyToNopcommerceSettings;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILocalizationService _localizationService;
        private readonly ILogger _logger;
        private readonly ISettingService _settingService;
        private readonly IUrlHelperFactory _urlHelperFactory;
        private readonly IActionContextAccessor _actionContextAccessor;

        #endregion

        #region Ctor

        public EtsyToNopcommercePlugin(EtsyToNopcommerceSettings etsyToNopcommerceSettings,
            IHttpClientFactory httpClientFactory,
            ILocalizationService localizationService,
            ILogger logger,
            ISettingService settingService, IUrlHelperFactory urlHelperFactory, IActionContextAccessor actionContextAccessor)
        {
            _etsyToNopcommerceSettings = etsyToNopcommerceSettings;
            _httpClientFactory = httpClientFactory;
            _localizationService = localizationService;
            _logger = logger;
            _settingService = settingService;
            _urlHelperFactory = urlHelperFactory;
            _actionContextAccessor = actionContextAccessor;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Gets currency live rates
        /// </summary>
        /// <param name="exchangeRateCurrencyCode">Exchange rate currency code</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the exchange rates
        /// </returns>
        
        //public async Task<IList<Core.Domain.Directory.ExchangeRate>> GetCurrencyLiveRatesAsync(string exchangeRateCurrencyCode)
        //{
        //    if (exchangeRateCurrencyCode == null)
        //        throw new ArgumentNullException(nameof(exchangeRateCurrencyCode));

        //    //add euro with rate 1
        //    var ratesToEuro = new List<Core.Domain.Directory.ExchangeRate>
        //    {
        //        new Core.Domain.Directory.ExchangeRate
        //        {
        //            CurrencyCode = "EUR",
        //            Rate = 1,
        //            UpdatedOn = DateTime.UtcNow
        //        }
        //    };

        //    //get exchange rates to euro from European Central Bank
        //    try
        //    {
        //        var httpClient = _httpClientFactory.CreateClient(NopHttpDefaults.DefaultHttpClient);
        //        var stream = await httpClient.GetStreamAsync(_ecbExchangeRateSettings.EcbLink);

        //        //load XML document
        //        var document = new XmlDocument();
        //        document.Load(stream);

        //        //add namespaces
        //        var namespaces = new XmlNamespaceManager(document.NameTable);
        //        namespaces.AddNamespace("ns", "http://www.ecb.int/vocabulary/2002-08-01/eurofxref");
        //        namespaces.AddNamespace("gesmes", "http://www.gesmes.org/xml/2002-08-01");

        //        //get daily rates
        //        var dailyRates = document.SelectSingleNode("gesmes:Envelope/ns:Cube/ns:Cube", namespaces);
        //        if (!DateTime.TryParseExact(dailyRates.Attributes["time"].Value, "yyyy-MM-dd", null, DateTimeStyles.None, out var updateDate))
        //            updateDate = DateTime.UtcNow;

        //        foreach (XmlNode currency in dailyRates.ChildNodes)
        //        {
        //            //get rate
        //            if (!decimal.TryParse(currency.Attributes["rate"].Value, NumberStyles.Currency, CultureInfo.InvariantCulture, out var currencyRate))
        //                continue;

        //            ratesToEuro.Add(new Core.Domain.Directory.ExchangeRate()
        //            {
        //                CurrencyCode = currency.Attributes["currency"].Value,
        //                Rate = currencyRate,
        //                UpdatedOn = updateDate
        //            });
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        await _logger.ErrorAsync("ECB exchange rate provider", ex);
        //    }

        //    //return result for the euro
        //    if (exchangeRateCurrencyCode.Equals("eur", StringComparison.InvariantCultureIgnoreCase))
        //        return ratesToEuro;

        //    //use only currencies that are supported by ECB
        //    var exchangeRateCurrency = ratesToEuro.FirstOrDefault(rate => rate.CurrencyCode.Equals(exchangeRateCurrencyCode, StringComparison.InvariantCultureIgnoreCase));
        //    if (exchangeRateCurrency == null)
        //        throw new NopException(await _localizationService.GetResourceAsync("Plugins.ExchangeRate.EcbExchange.Error"));

        //    //return result for the selected (not euro) currency
        //    return ratesToEuro.Select(rate => new Core.Domain.Directory.ExchangeRate
        //    {
        //        CurrencyCode = rate.CurrencyCode,
        //        Rate = Math.Round(rate.Rate / exchangeRateCurrency.Rate, 4),
        //        UpdatedOn = rate.UpdatedOn
        //    }).ToList();
        //}

        /// <summary>
        /// Install the plugin
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
        public override async Task InstallAsync()
        {
            //settings
            var defaultSettings = new EtsyToNopcommerceSettings
            {
                ConsumerKey = "uh3pwbu285jcynbsz50cadww",
                ConsumerSecret = "j6kxqxmu8c",
                RequestUrl= "https://www.etsy.com/oauth/connect",
                RequestAccessTokenUrl = "https://openapi.etsy.com/v3/public/oauth/token"
            };
            await _settingService.SaveSettingAsync(defaultSettings);

            //locales
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Misc.EtsyToNopcommerce.Test", "Test");

            await base.InstallAsync();
        }

        /// <summary>
        /// Uninstall the plugin
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
        public override async Task UninstallAsync()
        {
            //settings
            await _settingService.DeleteSettingAsync<EtsyToNopcommerceSettings>();

            //locales
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Misc.EtsyToNopcommerce.Test");

            await base.UninstallAsync();
        }

        public override string GetConfigurationPageUrl()
        {
            return _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext).RouteUrl("Plugin.Misc.EtsyToNopcommerce.Configure");
        }

        #endregion

    }
}
