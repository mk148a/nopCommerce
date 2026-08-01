using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using LinqToDB.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Localization;
using Nop.Core.Domain.Seo;
using Nop.Core.Domain.Stores;
using Nop.Core.Infrastructure;
using Nop.Plugin.Misc.GoogleShoppingMultiCountry.Services;
using Nop.Services.Catalog;
using Nop.Services.Cms;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Media;
using Nop.Services.Plugins;
using Nop.Services.Seo;
using Nop.Services.Tax;
using Nop.Web.Framework.Infrastructure;

namespace Nop.Plugin.Misc.GoogleShoppingMultiCountry
{
    /// <summary>
    /// Rename this file and change to the correct type
    /// </summary>
    public class GoogleShoppingMultiCountry : BasePlugin, IMiscPlugin
    {
        #region Fields

        private readonly CurrencySettings _currencySettings;
        private readonly GoogleShoppingMultiCountrySettings _googleShoppingMultiCountrySettings;
        private readonly IActionContextAccessor _actionContextAccessor;
        private readonly ICategoryService _categoryService;
        private readonly ICurrencyService _currencyService;
        private readonly IGoogleService _googleService;
        private readonly ILanguageService _languageService;
        private readonly ILocalizationService _localizationService;
        private readonly IManufacturerService _manufacturerService;
        private readonly IMeasureService _measureService;
        private readonly INopFileProvider _nopFileProvider;
        private readonly IPictureService _pictureService;
        private readonly IPriceCalculationService _priceCalculationService;
        private readonly IProductService _productService;
        private readonly ISettingService _settingService;
        private readonly IStoreContext _storeContext;
        private readonly ITaxService _taxService;
        private readonly IUrlHelperFactory _urlHelperFactory;
        private readonly IUrlRecordService _urlRecordService;
        private readonly IWebHelper _webHelper;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IWorkContext _workContext;
        private readonly MeasureSettings _measureSettings;

        #endregion
        #region Ctor
        public GoogleShoppingMultiCountry(CurrencySettings currencySettings,
            GoogleShoppingMultiCountrySettings googleShoppingMultiCountrySettings,
            IActionContextAccessor actionContextAccessor,
            ICategoryService categoryService,
            ICurrencyService currencyService,
            IGoogleService googleService,
            ILanguageService languageService,
            ILocalizationService localizationService,
            IManufacturerService manufacturerService,
            IMeasureService measureService,
            INopFileProvider nopFileProvider,
            IPictureService pictureService,
            IPriceCalculationService priceCalculationService,
            IProductService productService,
            ISettingService settingService,
            IStoreContext storeContext,
            ITaxService taxService,
            IUrlHelperFactory urlHelperFactory,
            IUrlRecordService urlRecordService,
            IWebHelper webHelper,
            IWebHostEnvironment webHostEnvironment,
            IWorkContext workContext,
            MeasureSettings measureSettings)

        {
            _actionContextAccessor = actionContextAccessor;
            _categoryService = categoryService;
            _currencyService = currencyService;
            _currencySettings = currencySettings;
            _googleService = googleService;
            _googleShoppingMultiCountrySettings= googleShoppingMultiCountrySettings;
            _languageService = languageService;
            _localizationService = localizationService;
            _manufacturerService = manufacturerService;
            _measureService = measureService;
            _measureSettings = measureSettings;
            _nopFileProvider = nopFileProvider;
            _pictureService = pictureService;
            _priceCalculationService = priceCalculationService;
            _productService = productService;
            _settingService = settingService;
            _storeContext = storeContext;
            _taxService = taxService;
            _urlHelperFactory = urlHelperFactory;
            _urlRecordService = urlRecordService;
            _webHelper = webHelper;
            _webHostEnvironment = webHostEnvironment;
            _workContext = workContext;
        }
        #endregion

        #region Utilities

        /// <summary>
        /// Removes invalid characters
        /// </summary>
        /// <param name="input">Input string</param>
        /// <param name="isHtmlEncoded">A value indicating whether input string is HTML encoded</param>
        /// <returns>Valid string</returns>
        protected virtual string StripInvalidChars(string input, bool isHtmlEncoded)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input;

            //Microsoft uses a proprietary encoding (called CP-1252) for the bullet symbol and some other special characters, 
            //whereas most websites and data feeds use UTF-8. When you copy-paste from a Microsoft product into a website, 
            //some characters may appear as junk. Our system generates data feeds in the UTF-8 character encoding, 
            //which many shopping engines now require.

            //http://www.atensoftware.com/p90.php?q=182

            if (isHtmlEncoded)
                input = WebUtility.HtmlDecode(input);

            input = input.Replace("¼", "");
            input = input.Replace("½", "");
            input = input.Replace("¾", "");
            //input = input.Replace("•", "");
            //input = input.Replace("”", "");
            //input = input.Replace("“", "");
            //input = input.Replace("’", "");
            //input = input.Replace("‘", "");
            //input = input.Replace("™", "");
            //input = input.Replace("®", "");
            //input = input.Replace("°", "");

            if (isHtmlEncoded)
                input = WebUtility.HtmlEncode(input);

            return input;
        }

        /// <summary>
        /// Get used currency
        /// </summary>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the Currency
        /// </returns>
        protected virtual async Task<Currency> GetUsedCurrencyAsync()
        {
            var currency = await _currencyService.GetCurrencyByIdAsync(_googleShoppingMultiCountrySettings.CurrencyId);
            if (currency == null || !currency.Published)
                currency = await _currencyService.GetCurrencyByIdAsync(_currencySettings.PrimaryStoreCurrencyId);
            return currency;
        }


        /// <summary>
        /// Get UrlHelper
        /// </summary>
        /// <returns>UrlHelper</returns>
        protected virtual IUrlHelper GetUrlHelper()
        {
            return _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext);
        }

        /// <summary>
        /// Get HTTP protocol
        /// </summary>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the Protocol name
        /// </returns>
        protected virtual async Task<string> GetHttpProtocolAsync()
        {
            return (await _storeContext.GetCurrentStoreAsync()).SslEnabled ? Uri.UriSchemeHttps : Uri.UriSchemeHttp;
        }

        protected virtual bool IsValidGtin(string gtin)
        {
            if (string.IsNullOrWhiteSpace(gtin) || gtin.Length is < 8 or > 14 || !gtin.All(char.IsDigit))
                return false;

            var sum = 0;
            for (var index = gtin.Length - 2; index >= 0; index--)
            {
                var positionFromRight = gtin.Length - 2 - index;
                var digit = gtin[index] - '0';
                sum += digit * (positionFromRight % 2 == 0 ? 3 : 1);
            }

            return (10 - sum % 10) % 10 == gtin[^1] - '0';
        }
        #endregion

        #region Methods

        // <summary>
        /// Install plugin
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
        public override async Task InstallAsync()
        {

            try
            {

                //settings
                var settings = new GoogleShoppingMultiCountrySettings
                {
                    PricesConsiderPromotions = false,
                    ProductPictureSize = 250,
                    PassShippingInfoWeight = false,
                    PassShippingInfoDimensions = false,
                    StaticFileName = $"googleshoppingmulticountry_{CommonHelper.GenerateRandomDigitCode(10)}.xml",
                    ExpirationNumberOfDays = 28
                };
                await _settingService.SaveSettingAsync(settings);

                //locales
                await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
                {
                    ["Plugins.Misc.GoogleShoppingMultiCountry.Store"] = "Store",
                    ["Plugins.Misc.GoogleShoppingMultiCountry.Store.Hint"] = "Select the store that will be used to generate the feed.",
                    ["Plugins.Misc.GoogleShoppingMultiCountry.Currency"] = "Currency",
                    ["Plugins.Misc.GoogleShoppingMultiCountry.Currency.Hint"] = "Select the default currency that will be used to generate the feed.",
                    ["Plugins.Misc.GoogleShoppingMultiCountry.DefaultGoogleCategory"] = "Default Google category",
                    ["Plugins.Misc.GoogleShoppingMultiCountry.DefaultGoogleCategory.Hint"] = "The default Google category to use if one is not specified.",
                    ["Plugins.Misc.GoogleShoppingMultiCountry.ExceptionLoadPlugin"] = "Cannot load the plugin",
                    ["Plugins.Misc.GoogleShoppingMultiCountry.General"] = "General",
                    ["Plugins.Misc.GoogleShoppingMultiCountry.GeneralInstructions"] = "<p><ul><li>At least two unique product identifiers are required. So each of your product should have manufacturer (brand) and MPN (manufacturer part number) specified</li><li>Specify default tax values in your Google Merchant Center account settings</li><li>Specify default shipping values in your Google Merchant Center account settings</li><li>In order to get more info about required fields look at the following article <a href=\"http://www.google.com/support/merchants/bin/answer.py?answer=188494\" target=\"_blank\">http://www.google.com/support/merchants/bin/answer.py?answer=188494</a></li></ul></p>",
                    ["Plugins.Misc.GoogleShoppingMultiCountry.Generate"] = "Generate feed",
                    ["Plugins.Misc.GoogleShoppingMultiCountry.Override"] = "Override product settings",
                    ["Plugins.Misc.GoogleShoppingMultiCountry.OverrideInstructions"] = "<p>You can download the list of allowed Google product category attributes <a href=\"http://www.google.com/support/merchants/bin/answer.py?answer=160081\" target=\"_blank\">here</a></p>",
                    ["Plugins.Misc.GoogleShoppingMultiCountry.PassShippingInfoWeight"] = "Pass shipping info (weight)",
                    ["Plugins.Misc.GoogleShoppingMultiCountry.PassShippingInfoWeight.Hint"] = "Check if you want to include shipping information (weight) in generated XML file.",
                    ["Plugins.Misc.GoogleShoppingMultiCountry.PassShippingInfoDimensions"] = "Pass shipping info (dimensions)",
                    ["Plugins.Misc.GoogleShoppingMultiCountry.PassShippingInfoDimensions.Hint"] = "Check if you want to include shipping information (dimensions) in generated XML file.",
                    ["Plugins.Misc.GoogleShoppingMultiCountry.PricesConsiderPromotions"] = "Prices consider promotions",
                    ["Plugins.Misc.GoogleShoppingMultiCountry.PricesConsiderPromotions.Hint"] = "Check if you want prices to be calculated with promotions (tier prices] = discounts] = special prices] = tax] = etc). But please note that it can significantly reduce time required to generate the feed file.",
                    ["Plugins.Misc.GoogleShoppingMultiCountry.ProductPictureSize"] = "Product thumbnail image size",
                    ["Plugins.Misc.GoogleShoppingMultiCountry.ProductPictureSize.Hint"] = "The default size (pixels) for product thumbnail images.",
                    ["Plugins.Misc.GoogleShoppingMultiCountry.Products.ProductName"] = "Product",
                    ["Plugins.Misc.GoogleShoppingMultiCountry.Products.ProductName.Hint"] = "Product Name",
                    ["Plugins.Misc.GoogleShoppingMultiCountry.Products.GoogleCategory"] = "Google Category",
                    ["Plugins.Misc.GoogleShoppingMultiCountry.Products.GoogleCategory.Hint"] = "Product category according to the Google product taxonomy.",
                    ["Plugins.Misc.GoogleShoppingMultiCountry.Products.GoogleCategoryId"] = "Google Category Id",
                    ["Plugins.Misc.GoogleShoppingMultiCountry.Products.GoogleCategory.Hint"] = "Product category Id according to the Google product taxonomy.",
                    ["Plugins.Misc.GoogleShoppingMultiCountry.Products.Gender"] = "Gender",
                    ["Plugins.Misc.GoogleShoppingMultiCountry.Products.Gender.Hint"] = "Gender of the people for whom the product is intended.",
                    ["Plugins.Misc.GoogleShoppingMultiCountry.Products.AgeGroup"] = "Age group",
                    ["Plugins.Misc.GoogleShoppingMultiCountry.Products.AgeGroup.Hint"] = "Age category of people for whom the goods are intended.",
                    ["Plugins.Misc.GoogleShoppingMultiCountry.Products.Color"] = "Color",
                    ["Plugins.Misc.GoogleShoppingMultiCountry.Products.Color.Hint"] = "Product color.",
                    ["Plugins.Misc.GoogleShoppingMultiCountry.Products.LanguageId"] = "Language Id",
                    ["Plugins.Misc.GoogleShoppingMultiCountry.Products.LanguageId.Hint"] = "Language Id.",
                    ["Plugins.Misc.GoogleShoppingMultiCountry.Products.Size"] = "Size",
                    ["Plugins.Misc.GoogleShoppingMultiCountry.Products.Size.Hint"] = "Product size.",
                    ["Plugins.Misc.GoogleShoppingMultiCountry.Products.CustomGoods"] = "Custom goods",
                    ["Plugins.Misc.GoogleShoppingMultiCountry.Products.CustomGoods.Hint"] = "Custom goods (no identifier exists).",
                    ["Plugins.Misc.GoogleShoppingMultiCountry.SuccessResult"] = "Google Shopping feed has been successfully generated.",
                    ["Plugins.Misc.GoogleShoppingMultiCountry.StaticFilePath"] = "Generated file path (static)",
                    ["Plugins.Misc.GoogleShoppingMultiCountry.StaticFilePath.Hint"] = "A file path of the generated file. It's static for your store and can be shared with the Google Shopping service.",
                    ["Plugins.Misc.GoogleShoppingMultiCountry.Products.CategoryName"] = "Category"

                });

                await base.InstallAsync();

            }
            catch (Exception e)
            {
                Console.WriteLine(e);

            }
        }

        /// <summary>
        /// Uninstall plugin
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
        public override async Task UninstallAsync()
        {
            //settings
            await _settingService.DeleteSettingAsync<GoogleShoppingMultiCountrySettings>();

            //locales
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Misc.GoogleShoppingMultiCountry");

            await base.UninstallAsync();
        }
        /// <summary>
        /// Gets a configuration page URL
        /// </summary>
        public override string GetConfigurationPageUrl()
        {
            //return _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext).RouteUrl(AccessiBeDefaults.ConfigurationRouteName);
            //return $"{_webHelper.GetStoreLocation()}Admin/PaymentIyzico/Configure";
            return _webHelper.GetStoreLocation() + "Admin/GoogleShoppingMultiCountry/Configure";


        }
        /// <summary>
        /// Generate a static feed file
        /// </summary>
        /// <param name="store">Store</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public virtual async Task GenerateStaticFileAsync(Store store)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));
            foreach (var language in await _languageService.GetAllLanguagesAsync(false,store.Id))
            {
                var filePath = _nopFileProvider.Combine(_webHostEnvironment.WebRootPath, "files", "exportimport", store.Id + "-"+language.UniqueSeoCode +"-"+ _googleShoppingMultiCountrySettings.StaticFileName);
                using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                await GenerateFeedAsync(fs, store, language);
            }
         
        }
        /// <summary>
        /// Generate a feed
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <param name="store">Store</param>
        /// <returns>Generated feed</returns>
        public async Task GenerateFeedAsync(Stream stream, Store store,Language lang)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (store == null)
                throw new ArgumentNullException(nameof(store));

            const string googleBaseNamespace = "http://base.google.com/ns/1.0";

            var settings = new XmlWriterSettings
            {
                Encoding = Encoding.UTF8,
                Indent = true,
                Async = true
            };

            var googleShoppingSettings = await _settingService.LoadSettingAsync<GoogleShoppingMultiCountrySettings>(store.Id);

           

            //we load all Google products here using one SQL request (performance optimization)
            var allGoogleProducts = await _googleService.GetAllAsync();

            using var writer = XmlWriter.Create(stream, settings);
            //Generate feed according to the following specs: http://www.google.com/support/merchants/bin/answer.py?answer=188494&expand=GB
           await writer.WriteStartDocumentAsync();
            writer.WriteStartElement("rss");
            writer.WriteAttributeString("version", "2.0");
           await writer.WriteAttributeStringAsync("xmlns", "g", null, googleBaseNamespace);
            writer.WriteStartElement("channel");
            writer.WriteElementString("title", "Google Base feed");
            writer.WriteElementString("link", "http://base.google.com/base/");
            writer.WriteElementString("description", "Information about products");

            var products = await _productService.SearchProductsAsync(storeId: store.Id, visibleIndividuallyOnly: true,languageId:lang.Id);
            var systemProductSkus = (await _settingService.GetSettingByKeyAsync<string>(
                    "FixedByWeightByTotalSettings.SystemProductSkus", "expresshipping"))
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var productsForIdentifiers = await _productService.SearchProductsAsync(storeId: store.Id, languageId: lang.Id);
            var gtinCounts = productsForIdentifiers
                .Where(product => !string.IsNullOrWhiteSpace(product.Gtin))
                .GroupBy(product => product.Gtin, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
            foreach (var product in products)
            {
                var productsToProcess = new List<Product>();
                switch (product.ProductType)
                {
                    case ProductType.SimpleProduct:
                        {
                            //simple product doesn't have child products
                            productsToProcess.Add(product);
                        }
                        break;
                    case ProductType.GroupedProduct:
                        {
                            //grouped products could have several child products
                            var associatedProducts = await _productService.GetAssociatedProductsAsync(product.Id, store.Id);
                            productsToProcess.AddRange(associatedProducts);
                        }
                        break;
                    default:
                        continue;
                }
                foreach (var productToProcess in productsToProcess)
                {
                    if (systemProductSkus.Contains(productToProcess.Sku, StringComparer.OrdinalIgnoreCase))
                        continue;

                    writer.WriteStartElement("item");

                    #region Basic Product Information

                    //id [id]- An identifier of the item
                   await writer.WriteElementStringAsync("g", "id", googleBaseNamespace, productToProcess.Id.ToString());

                    //title [title] - Title of the item
                    writer.WriteStartElement("title");
                    var title = await _localizationService.GetLocalizedAsync(productToProcess, x => x.Name, lang.Id);
                    //title should be not longer than 70 characters
                    if (title.Length > 70)
                        title = title[..70];
                   await writer.WriteCDataAsync(title);
                  await  writer.WriteEndElementAsync(); // title

                    //description [description] - Description of the item
                    writer.WriteStartElement("description");
                    var description = await _localizationService.GetLocalizedAsync(productToProcess, x => x.FullDescription, lang.Id);
                    if (string.IsNullOrEmpty(description))
                        description = await _localizationService.GetLocalizedAsync(productToProcess, x => x.ShortDescription, lang.Id);
                    if (string.IsNullOrEmpty(description))
                        description = await _localizationService.GetLocalizedAsync(productToProcess, x => x.Name, lang.Id); //description is required
                                                                                                                      //resolving character encoding issues in your data feed
                    description = StripInvalidChars(description, true);
                   await writer.WriteCDataAsync(description);
                   await writer.WriteEndElementAsync(); // description

                    //google product category [google_product_category] - Google's category of the item
                    //the category of the product according to Google’s product taxonomy. http://www.google.com/support/merchants/bin/answer.py?answer=160081
                    var googleProductCategory = "";
                    // Feed child products from their own catalog record, not their grouped parent.
                    var googleProduct = allGoogleProducts.FirstOrDefault(x => x.ProductId == productToProcess.Id);
                    if (googleProduct != null)
                        googleProductCategory = googleProduct.Taxonomy; //Todo:Buraya taxonomy Id alma özelliği ekle
                    if (string.IsNullOrEmpty(googleProductCategory))
                        googleProductCategory = googleShoppingSettings.DefaultGoogleCategoryId;
                    if (string.IsNullOrEmpty(googleProductCategory))
                        throw new NopException("Default Google category is not set");
                  await  writer.WriteStartElementAsync("g", "google_product_category", googleBaseNamespace);
                  await  writer.WriteCDataAsync(googleProductCategory);
                  await  writer.WriteFullEndElementAsync(); // g:google_product_category

                    //product type [product_type] - Your category of the item
                    var defaultProductCategory = (await _categoryService
                        .GetProductCategoriesByProductIdAsync(productToProcess.Id))
                        .FirstOrDefault();
                    if (defaultProductCategory != null)
                    {
                        //TODO localize categories
                        var category = await _categoryService.GetFormattedBreadCrumbAsync(
                            category: await _categoryService.GetCategoryByIdAsync(defaultProductCategory.CategoryId),
                            separator: ">",
                            languageId: lang.Id);
                        if (!string.IsNullOrEmpty(category))
                        {
                         await   writer.WriteStartElementAsync("g", "product_type", googleBaseNamespace);
                          await  writer.WriteCDataAsync(category);
                          await  writer.WriteFullEndElementAsync(); // g:product_type
                        }
                    }

              
                    var urlHelper = GetUrlHelper();
                    

                    var productUrl = urlHelper.RouteUrl("Product", new { SeName = await _urlRecordService.GetSeNameAsync(productToProcess, languageId: lang.Id) }, await GetHttpProtocolAsync());
                    var pathBase = urlHelper.ActionContext.HttpContext.Request.PathBase;
                    var scheme = new Uri(productUrl).GetComponents(UriComponents.SchemeAndServer,
                        UriFormat.Unescaped);
                    var path = new Uri(productUrl).PathAndQuery;
                    var localizedPath = path
                        .RemoveLanguageSeoCodeFromUrl(pathBase, true)
                        .AddLanguageSeoCodeToUrl(pathBase, true, lang);
                    var localizedUrl = new Uri(new Uri(scheme), localizedPath).ToString();
                    writer.WriteElementString("link", localizedUrl);

                    //image link [image_link] - URL of an image of the item
                    //additional images [additional_image_link]
                    //up to 10 pictures
                    const int maximumPictures = 10;
                    var storeLocation = _webHelper.GetStoreLocation();
                    var pictures = await _pictureService.GetPicturesByProductIdAsync(productToProcess.Id, maximumPictures);
                    for (var i = 0; i < pictures.Count; i++)
                    {
                        var picture = pictures[i];
                        var imageUrl = await _pictureService.GetPictureUrlAsync(picture.Id,
                            googleShoppingSettings.ProductPictureSize,
                            storeLocation: storeLocation);

                        if (i == 0)
                        {
                            //default image
                         await   writer.WriteElementStringAsync("g", "image_link", googleBaseNamespace, imageUrl);
                        }
                        else
                        {
                            //additional image
                          await  writer.WriteElementStringAsync("g", "additional_image_link", googleBaseNamespace, imageUrl);
                        }
                    }
                    if (!pictures.Any())
                    {
                        //no picture? submit a default one
                        var imageUrl = await _pictureService.GetDefaultPictureUrlAsync(googleShoppingSettings.ProductPictureSize, storeLocation: storeLocation);
                      await  writer.WriteElementStringAsync("g", "image_link", googleBaseNamespace, imageUrl);
                    }

                    //condition [condition] - Condition or state of the item
                  await  writer.WriteElementStringAsync("g", "condition", googleBaseNamespace, "new");

                 //await  writer.WriteElementStringAsync("g", "expiration_date", googleBaseNamespace, DateTime.Now.AddDays(googleShoppingSettings.ExpirationNumberOfDays).ToString("yyyy-MM-dd"));

                    #endregion

                    #region Availability & Price

                    //availability [availability] - Availability status of the item
                    var availability = "in stock"; //in stock by default
                    if (productToProcess.ManageInventoryMethod == ManageInventoryMethod.ManageStock
                        && productToProcess.BackorderMode == BackorderMode.NoBackorders
                        && await _productService.GetTotalStockQuantityAsync(productToProcess) <= 0)
                    {
                        availability = "out of stock";
                    }
                    //uncomment th code below in order to support "preorder" value for "availability"
                    //if (product.AvailableForPreOrder &&
                    //    (!product.PreOrderAvailabilityStartDateTimeUtc.HasValue || 
                    //    product.PreOrderAvailabilityStartDateTimeUtc.Value >= DateTime.UtcNow))
                    //{
                    //    availability = "preorder";
                    //}
                  await  writer.WriteElementStringAsync("g", "availability", googleBaseNamespace, availability);

                    //price [price] - Price of the item
                    var currency =await _currencyService.GetCurrencyByIdAsync(lang.DefaultCurrencyId);
                    if (currency==null)
                    {
                        var currencies =await _currencyService.GetAllCurrenciesAsync(false, store.Id);
                       var currencyIsAvailableForThisCulture= currencies.Any(x => x.DisplayLocale == lang.LanguageCulture);
                       if (currencyIsAvailableForThisCulture)
                       {
                           currency = currencies.SingleOrDefault(x => x.DisplayLocale == lang.LanguageCulture);
                       }
                       else
                       {
                           throw new Exception("Please configure your default currencies of your languages");
                       }
                    }
                    decimal finalPriceBase;
                    if (googleShoppingSettings.PricesConsiderPromotions)
                    {
                        var currentCustomer = await _workContext.GetCurrentCustomerAsync();

                        //calculate price for the maximum quantity if we have tier prices, and choose minimal
                        var minPossiblePrice = (await _priceCalculationService.GetFinalPriceAsync(productToProcess, currentCustomer, store, quantity: int.MaxValue)).finalPrice;

                        finalPriceBase = (await _taxService.GetProductPriceAsync(productToProcess, minPossiblePrice)).price;
                    }
                    else
                    {
                        finalPriceBase = productToProcess.Price;
                    }
                    var price = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(finalPriceBase, currency);
                    //round price now so it matches the product details page
                    price = await _priceCalculationService.RoundPriceAsync(price);

                    // Merchant XML requires a culture-invariant numeric price; the configured language must not turn 12.50 into 12,50.
                    await writer.WriteElementStringAsync("g", "price", googleBaseNamespace,
                        price.ToString("0.00", CultureInfo.InvariantCulture) + " " + currency.CurrencyCode);

                    #endregion

                    #region Unique Product Identifiers

                    /* Unique product identifiers such as UPC, EAN, JAN or ISBN allow us to show your listing on the appropriate product page. If you don't provide the required unique product identifiers, your store may not appear on product pages, and all your items may be removed from Product Search.
                     * We require unique product identifiers for all products - except for custom made goods. For apparel, you must submit the 'brand' attribute. For media (such as books, movies, music and video games), you must submit the 'gtin' attribute. In all cases, we recommend you submit all three attributes.
                     * You need to submit at least two attributes of 'brand', 'gtin' and 'mpn', but we recommend that you submit all three if available. For media (such as books, movies, music and video games), you must submit the 'gtin' attribute, but we recommend that you include 'brand' and 'mpn' if available.
                    */

                    //GTIN [gtin] - GTIN
                    var gtin = productToProcess.Gtin;
                    if (IsValidGtin(gtin) && gtinCounts.TryGetValue(gtin, out var gtinCount) && gtinCount == 1)
                    {
                       await writer.WriteStartElementAsync("g", "gtin", googleBaseNamespace);
                       await writer.WriteCDataAsync(gtin);
                       await writer.WriteFullEndElementAsync(); // g:gtin
                    }

                    //brand [brand] - Brand of the item
                    var defaultManufacturer = (await _manufacturerService.GetProductManufacturersByProductIdAsync(productToProcess.Id)).FirstOrDefault();
                    if (defaultManufacturer != null)
                    {
                      await  writer.WriteStartElementAsync("g", "brand", googleBaseNamespace);
                      await  writer.WriteCDataAsync((await _manufacturerService.GetManufacturerByIdAsync(defaultManufacturer.ManufacturerId))?.Name);
                      await  writer.WriteFullEndElementAsync(); // g:brand
                    }

                    //mpn [mpn] - Manufacturer Part Number (MPN) of the item
                    var mpn = productToProcess.ManufacturerPartNumber;
                    if (!string.IsNullOrEmpty(mpn))
                    {
                      await  writer.WriteStartElementAsync("g", "mpn", googleBaseNamespace);
                      await  writer.WriteCDataAsync(mpn);
                     await   writer.WriteFullEndElementAsync(); // g:mpn
                    }

                    //identifier exists [identifier_exists] - Submit custom goods
                    if (googleProduct != null && googleProduct.CustomGoods)
                    {
                      await  writer.WriteElementStringAsync("g", "identifier_exists", googleBaseNamespace, "FALSE");
                    }

                    #endregion

                    #region Apparel Products

                    /* Apparel includes all products that fall under 'Apparel & Accessories' (including all sub-categories)
                     * in Google’s product taxonomy.
                    */

                    //gender [gender] - Gender of the item
                    if (googleProduct != null && !string.IsNullOrEmpty(googleProduct.Gender))
                    {
                     await   writer.WriteStartElementAsync("g", "gender", googleBaseNamespace);
                      await  writer.WriteCDataAsync(googleProduct.Gender);
                      await  writer.WriteFullEndElementAsync(); // g:gender
                    }

                    //age group [age_group] - Target age group of the item
                    if (googleProduct != null && !string.IsNullOrEmpty(googleProduct.AgeGroup))
                    {
                      await  writer.WriteStartElementAsync("g", "age_group", googleBaseNamespace);
                      await  writer.WriteCDataAsync(googleProduct.AgeGroup);
                      await  writer.WriteFullEndElementAsync(); // g:age_group
                    }

                    //color [color] - Color of the item
                    if (googleProduct != null && !string.IsNullOrEmpty(googleProduct.Color))
                    {
                      await  writer.WriteStartElementAsync("g", "color", googleBaseNamespace);
                      await  writer.WriteCDataAsync(googleProduct.Color);
                     await   writer.WriteFullEndElementAsync(); // g:color
                    }

                    //size [size] - Size of the item
                    if (googleProduct != null && !string.IsNullOrEmpty(googleProduct.Size))
                    {
                      await  writer.WriteStartElementAsync("g", "size", googleBaseNamespace);
                      await  writer.WriteCDataAsync(googleProduct.Size);
                      await  writer.WriteFullEndElementAsync(); // g:size
                    }

                    #endregion

                    #region Tax & Shipping

                    //tax [tax]
                    //The tax attribute is an item-level override for merchant-level tax settings as defined in your Google Merchant Center account. This attribute is only accepted in the US, if your feed targets a country outside of the US, please do not use this attribute.
                    //IMPORTANT NOTE: Set tax in your Google Merchant Center account settings

                    //IMPORTANT NOTE: Set shipping in your Google Merchant Center account settings

                    //shipping weight [shipping_weight] - Weight of the item for shipping
                    //We accept only the following units of weight: lb, oz, g, kg.
                    if (googleShoppingSettings.PassShippingInfoWeight)
                    {
                        var shippingWeight = productToProcess.Weight;
                        var weightSystemName = (await _measureService.GetMeasureWeightByIdAsync(_measureSettings.BaseWeightId)).SystemKeyword;
                        var weightName = weightSystemName switch
                        {
                            "ounce" => "oz",
                            "lb" => "lb",
                            "grams" => "g",
                            "kg" => "kg",
                            _ => throw new Exception("Not supported weight. Google accepts the following units: lb, oz, g, kg."),
                        };
                     await   writer.WriteElementStringAsync("g", "shipping_weight", googleBaseNamespace, string.Format(CultureInfo.InvariantCulture, "{0} {1}", shippingWeight.ToString(CultureInfo.InvariantCulture), weightName));
                    }

                    //shipping length [shipping_length] - Length of the item for shipping
                    //shipping width [shipping_width] - Width of the item for shipping
                    //shipping height [shipping_height] - Height of the item for shipping
                    //We accept only the following units of length: in, cm
                    if (googleShoppingSettings.PassShippingInfoDimensions)
                    {
                        var length = productToProcess.Length;
                        var width = productToProcess.Width;
                        var height = productToProcess.Height;
                        var dimensionSystemName = (await _measureService.GetMeasureDimensionByIdAsync(_measureSettings.BaseDimensionId)).SystemKeyword;
                        var dimensionName = dimensionSystemName switch
                        {
                            "inches" => "in",
                            //TODO support other dimensions (convert to cm)
                            _ => "cm", //unknown dimension 
                        };
                        await writer.WriteElementStringAsync("g", "shipping_length", googleBaseNamespace, string.Format(CultureInfo.InvariantCulture, "{0} {1}", length.ToString(CultureInfo.InvariantCulture), dimensionName));
                        await writer.WriteElementStringAsync("g", "shipping_width", googleBaseNamespace, string.Format(CultureInfo.InvariantCulture, "{0} {1}", width.ToString(CultureInfo.InvariantCulture), dimensionName));
                        await writer.WriteElementStringAsync("g", "shipping_height", googleBaseNamespace, string.Format(CultureInfo.InvariantCulture, "{0} {1}", height.ToString(CultureInfo.InvariantCulture), dimensionName));
                    }

                    #endregion

                  await  writer.WriteEndElementAsync(); // item
                }
            }

            await writer.WriteEndElementAsync(); // channel
            await writer.WriteEndElementAsync(); // rss
            await writer.WriteEndDocumentAsync();
        }

        #endregion
    }
}
