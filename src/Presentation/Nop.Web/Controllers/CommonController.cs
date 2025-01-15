using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Nop.Core;
using Nop.Core.Domain;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Localization;
using Nop.Core.Domain.Security;
using Nop.Core.Domain.Tax;
using Nop.Core.Domain.Vendors;
using Nop.Services.Common;
using Nop.Services.Directory;
using Nop.Services.Html;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Messages;
using Nop.Services.Vendors;
using Nop.Web.Factories;
using Nop.Web.Framework.Mvc.Filters;
using Nop.Web.Framework.Themes;
using Nop.Web.Models.Common;

namespace Nop.Web.Controllers
{
    [AutoValidateAntiforgeryToken]
    public partial class CommonController : BasePublicController
    {
        #region Fields

        private readonly CaptchaSettings _captchaSettings;
        private readonly CommonSettings _commonSettings;
        private readonly ICommonModelFactory _commonModelFactory;
        private readonly ICurrencyService _currencyService;
        private readonly ICustomerActivityService _customerActivityService;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly IHtmlFormatter _htmlFormatter;
        private readonly ILanguageService _languageService;
        private readonly ILocalizationService _localizationService;
        private readonly IStoreContext _storeContext;
        private readonly IThemeContext _themeContext;
        private readonly IVendorService _vendorService;
        private readonly IWorkContext _workContext;
        private readonly IWorkflowMessageService _workflowMessageService;
        private readonly LocalizationSettings _localizationSettings;
        private readonly SitemapSettings _sitemapSettings;
        private readonly SitemapXmlSettings _sitemapXmlSettings;
        private readonly StoreInformationSettings _storeInformationSettings;
        private readonly VendorSettings _vendorSettings;

        #endregion

        #region Ctor

        public CommonController(CaptchaSettings captchaSettings,
            CommonSettings commonSettings,
            ICommonModelFactory commonModelFactory,
            ICurrencyService currencyService,
            ICustomerActivityService customerActivityService,
            IGenericAttributeService genericAttributeService,
            IHtmlFormatter htmlFormatter,
            ILanguageService languageService,
            ILocalizationService localizationService,
            IStoreContext storeContext,
            IThemeContext themeContext,
            IVendorService vendorService,
            IWorkContext workContext,
            IWorkflowMessageService workflowMessageService,
            LocalizationSettings localizationSettings,
            SitemapSettings sitemapSettings,
            SitemapXmlSettings sitemapXmlSettings,
            StoreInformationSettings storeInformationSettings,
            VendorSettings vendorSettings)
        {
            _captchaSettings = captchaSettings;
            _commonSettings = commonSettings;
            _commonModelFactory = commonModelFactory;
            _currencyService = currencyService;
            _customerActivityService = customerActivityService;
            _genericAttributeService = genericAttributeService;
            _htmlFormatter = htmlFormatter;
            _languageService = languageService;
            _localizationService = localizationService;
            _storeContext = storeContext;
            _themeContext = themeContext;
            _vendorService = vendorService;
            _workContext = workContext;
            _workflowMessageService = workflowMessageService;
            _localizationSettings = localizationSettings;
            _sitemapSettings = sitemapSettings;
            _sitemapXmlSettings = sitemapXmlSettings;
            _storeInformationSettings = storeInformationSettings;
            _vendorSettings = vendorSettings;
        }

        #endregion

        #region Methods

        //page not found
        public virtual IActionResult PageNotFound()
        {
            Response.StatusCode = 404;
            Response.ContentType = "text/html";

            return View();
        }

        //available even when a store is closed
        [CheckAccessClosedStore(true)]
        //available even when navigation is not allowed
        [CheckAccessPublicStore(true)]
        public virtual async Task<IActionResult> SetLanguage(int langid, string returnUrl = "")
        {
            var language = await _languageService.GetLanguageByIdAsync(langid);
            if (!language?.Published ?? false)
                language = await _workContext.GetWorkingLanguageAsync();

            //home page
            if (string.IsNullOrEmpty(returnUrl))
                returnUrl = Url.RouteUrl("Homepage");

            //language part in URL
            if (_localizationSettings.SeoFriendlyUrlsForLanguagesEnabled)
            {
                //remove current language code if it's already localized URL
                if ((await returnUrl.IsLocalizedUrlAsync(Request.PathBase, true)).IsLocalized)
                    returnUrl = returnUrl.RemoveLanguageSeoCodeFromUrl(Request.PathBase, true);

                //and add code of passed language
                returnUrl = returnUrl.AddLanguageSeoCodeToUrl(Request.PathBase, true, language);
            }

            await _workContext.SetWorkingLanguageAsync(language);

            //prevent open redirection attack
            if (!Url.IsLocalUrl(returnUrl))
                returnUrl = Url.RouteUrl("Homepage");

            return Redirect(returnUrl);
        }

        //available even when navigation is not allowed
        [CheckAccessPublicStore(true)]
        public virtual async Task<IActionResult> SetCurrency(int customerCurrency, string returnUrl = "")
        {
            var currency = await _currencyService.GetCurrencyByIdAsync(customerCurrency);
            if (currency != null)
                await _workContext.SetWorkingCurrencyAsync(currency);

            //home page
            if (string.IsNullOrEmpty(returnUrl))
                returnUrl = Url.RouteUrl("Homepage");

            //prevent open redirection attack
            if (!Url.IsLocalUrl(returnUrl))
                returnUrl = Url.RouteUrl("Homepage");

            return Redirect(returnUrl);
        }

        //available even when navigation is not allowed
        [CheckAccessPublicStore(true)]
        public virtual async Task<IActionResult> SetTaxType(int customerTaxType, string returnUrl = "")
        {
            var taxDisplayType = (TaxDisplayType)Enum.ToObject(typeof(TaxDisplayType), customerTaxType);
            await _workContext.SetTaxDisplayTypeAsync(taxDisplayType);

            //home page
            if (string.IsNullOrEmpty(returnUrl))
                returnUrl = Url.RouteUrl("Homepage");

            //prevent open redirection attack
            if (!Url.IsLocalUrl(returnUrl))
                returnUrl = Url.RouteUrl("Homepage");

            return Redirect(returnUrl);
        }

        //contact us page
        //available even when a store is closed
        [CheckAccessClosedStore(true)]
        public virtual async Task<IActionResult> ContactUs()
        {
            var model = new ContactUsModel();
            model = await _commonModelFactory.PrepareContactUsModelAsync(model, false);
            
            return View(model);
        }
        private async Task<bool> CheckSpamAsync(string email)
        {
            try
            {
                var apiUrl = $"http://api.stopforumspam.org/api?email={email}";
                var httpClient = new HttpClient();
                var response = await httpClient.GetStringAsync(apiUrl);

                var xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(response);

                var appearsNode = xmlDoc.SelectSingleNode("//appears");
                return appearsNode?.InnerText == "yes";
            }
            catch 
            {

                return false;
            }
           
        }
        private async Task AddIpToBlackListAsync(string ipAddress)
        {
            // Veritabanı bağlantısı ve sorgu
            var connectionString = "Data Source=.;Initial Catalog=HoodArcheryShopV450bugfixLancelotDb;Integrated Security=False;Persist Security Info=False;User ID=Murat;Password=1234567890aA+;Trust Server Certificate=True;Max Pool Size=200";
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                // Önce IP adresinin BlackList'te olup olmadığını kontrol et
                var checkQuery = "SELECT COUNT(*) FROM [IpBlockerNetcore].[dbo].[BlackList] WHERE IpAdresi = @IpAdresi";
                using (var checkCommand = new SqlCommand(checkQuery, connection))
                {
                    checkCommand.Parameters.AddWithValue("@IpAdresi", ipAddress);
                    var existingCount = (int)await checkCommand.ExecuteScalarAsync();

                    // Eğer IP adresi zaten BlackList'teyse, ekleme yapma
                    if (existingCount > 0)
                    {
                        return;
                    }
                }

                // IP adresi BlackList'te yoksa, ekle
                var insertQuery = @"
            INSERT INTO [IpBlockerNetcore].[dbo].[BlackList] (DangerLevel, Date, IpAdresi, DomainName, Country)
            VALUES (@DangerLevel, @Date, @IpAdresi, @DomainName, @Country)";

                using (var insertCommand = new SqlCommand(insertQuery, connection))
                {
                    insertCommand.Parameters.AddWithValue("@DangerLevel", 100); // DangerLevel = 100
                    insertCommand.Parameters.AddWithValue("@Date", DateTime.UtcNow);
                    insertCommand.Parameters.AddWithValue("@IpAdresi", ipAddress);
                    insertCommand.Parameters.AddWithValue("@DomainName", DBNull.Value); // DomainName boş bırakıldı
                    insertCommand.Parameters.AddWithValue("@Country", "Spammer"); // Country olarak "Spammer" eklendi

                    await insertCommand.ExecuteNonQueryAsync();
                }
            }
        }
        private async Task RemoveIpFromWhiteListAsync(string ipAddress)
        {
            // Veritabanı bağlantısı ve sorgu
            var connectionString = "Data Source=.;Initial Catalog=HoodArcheryShopV450bugfixLancelotDb;Integrated Security=False;Persist Security Info=False;User ID=Murat;Password=1234567890aA+;Trust Server Certificate=True;Max Pool Size=200";
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                var query = "DELETE FROM [IpBlockerNetcore].[dbo].[WhiteList] WHERE IpAdresi = @IpAdresi";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@IpAdresi", ipAddress);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        [HttpPost, ActionName("ContactUs")]
        [ValidateCaptcha]
        [CheckAccessClosedStore(true)]
        public virtual async Task<IActionResult> ContactUsSend(ContactUsModel model, bool captchaValid)
        {
            // CAPTCHA doğrulaması
            if (_captchaSettings.Enabled && _captchaSettings.ShowOnContactUsPage && !captchaValid)
            {
                ModelState.AddModelError("", await _localizationService.GetResourceAsync("Common.WrongCaptchaMessage"));
            }

            model = await _commonModelFactory.PrepareContactUsModelAsync(model, true);

            if (ModelState.IsValid)
            {
                var subject = _commonSettings.SubjectFieldOnContactUsForm ? model.Subject : null;
                var body = _htmlFormatter.FormatText(model.Enquiry, false, true, false, false, false, false);

                try
                {
                    // Müşteri ve IP adresini al
                    var customer = await _workContext.GetCurrentCustomerAsync();
                    var ipAddress = customer.LastIpAddress;

                    if (ipAddress != null)
                    {
                        // StopForumSpam API'si ile e-posta kontrolü
                        var isSpam = await CheckSpamAsync(model.Email);
                        Console.WriteLine(customer.LastIpAddress + " nolu ipye ait spam sonucu:"+ isSpam+" mail adresi:"+ model.Email);
                        if (isSpam)
                        {
                            // IP adresini BlackList'e ekle
                            await AddIpToBlackListAsync(ipAddress);

                            // Eğer IP adresi WhiteList'te varsa, onu kaldır
                            await RemoveIpFromWhiteListAsync(ipAddress);

                            // E-postayı gönderme ve kullanıcıya bilgi ver
                            model.SuccessfullySent = false;
                            model.Result = "Spam Detected";

                            return View(model);
                        }
                    }

                    // Eğer spam değilse, e-postayı gönder
                    await _workflowMessageService.SendContactUsMessageAsync(
                        (await _workContext.GetWorkingLanguageAsync()).Id,
                        model.Email.Trim(),
                        model.FullName,
                        subject,
                        body
                    );

                    model.SuccessfullySent = true;
                    model.Result = await _localizationService.GetResourceAsync("ContactUs.YourEnquiryHasBeenSent");

                    // Aktivite logu
                    await _customerActivityService.InsertActivityAsync(
                        "PublicStore.ContactUs",
                        await _localizationService.GetResourceAsync("ActivityLog.PublicStore.ContactUs")
                    );

                    return View(model);
                }
                catch (Exception ex)
                {
                    // Hata durumunda loglama yapabilirsiniz
                     Console.WriteLine("ContactUsSend error", ex);
                    model.SuccessfullySent = false;
                    model.Result = "Error";

                    return View(model);
                }
            }

            return View(model);
        }

        //contact vendor page
        public virtual async Task<IActionResult> ContactVendor(int vendorId)
        {
            if (!_vendorSettings.AllowCustomersToContactVendors)
                return RedirectToRoute("Homepage");

            var vendor = await _vendorService.GetVendorByIdAsync(vendorId);
            if (vendor == null || !vendor.Active || vendor.Deleted)
                return RedirectToRoute("Homepage");

            var model = new ContactVendorModel();
            model = await _commonModelFactory.PrepareContactVendorModelAsync(model, vendor, false);
            
            return View(model);
        }

        [HttpPost, ActionName("ContactVendor")]        
        [ValidateCaptcha]
        public virtual async Task<IActionResult> ContactVendorSend(ContactVendorModel model, bool captchaValid)
        {
            if (!_vendorSettings.AllowCustomersToContactVendors)
                return RedirectToRoute("Homepage");

            var vendor = await _vendorService.GetVendorByIdAsync(model.VendorId);
            if (vendor == null || !vendor.Active || vendor.Deleted)
                return RedirectToRoute("Homepage");

            //validate CAPTCHA
            if (_captchaSettings.Enabled && _captchaSettings.ShowOnContactUsPage && !captchaValid)
            {
                ModelState.AddModelError("", await _localizationService.GetResourceAsync("Common.WrongCaptchaMessage"));
            }

            model = await _commonModelFactory.PrepareContactVendorModelAsync(model, vendor, true);

            if (ModelState.IsValid)
            {
                var subject = _commonSettings.SubjectFieldOnContactUsForm ? model.Subject : null;
                var body = _htmlFormatter.FormatText(model.Enquiry, false, true, false, false, false, false);

                await _workflowMessageService.SendContactVendorMessageAsync(vendor, (await _workContext.GetWorkingLanguageAsync()).Id,
                    model.Email.Trim(), model.FullName, subject, body);

                model.SuccessfullySent = true;
                model.Result = await _localizationService.GetResourceAsync("ContactVendor.YourEnquiryHasBeenSent");

                return View(model);
            }

            return View(model);
        }

        //sitemap page
        public virtual async Task<IActionResult> Sitemap(SitemapPageModel pageModel)
        {
            if (!_sitemapSettings.SitemapEnabled)
                return RedirectToRoute("Homepage");

            var model = await _commonModelFactory.PrepareSitemapModelAsync(pageModel);
            
            return View(model);
        }

        //SEO sitemap page
        //available even when a store is closed
        [CheckAccessClosedStore(true)]
        //ignore SEO friendly URLs checks
        [CheckLanguageSeoCode(true)]
        public virtual async Task<IActionResult> SitemapXml(int? id)
        {
            var siteMap = _sitemapXmlSettings.SitemapXmlEnabled
                ? await _commonModelFactory.PrepareSitemapXmlAsync(id) : string.Empty;

            return Content(siteMap, "text/xml");
        }

        public virtual async Task<IActionResult> SetStoreTheme(string themeName, string returnUrl = "")
        {
            await _themeContext.SetWorkingThemeNameAsync(themeName);

            //home page
            if (string.IsNullOrEmpty(returnUrl))
                returnUrl = Url.RouteUrl("Homepage");

            //prevent open redirection attack
            if (!Url.IsLocalUrl(returnUrl))
                returnUrl = Url.RouteUrl("Homepage");

            return Redirect(returnUrl);
        }

        [HttpPost]
        //available even when a store is closed
        [CheckAccessClosedStore(true)]
        //available even when navigation is not allowed
        [CheckAccessPublicStore(true)]
        public virtual async Task<IActionResult> EuCookieLawAccept()
        {
            if (!_storeInformationSettings.DisplayEuCookieLawWarning)
                //disabled
                return Json(new { stored = false });

            //save setting
            var store = await _storeContext.GetCurrentStoreAsync();
            await _genericAttributeService.SaveAttributeAsync(await _workContext.GetCurrentCustomerAsync(), NopCustomerDefaults.EuCookieLawAcceptedAttribute, true, store.Id);
            return Json(new { stored = true });
        }

        //robots.txt file
        //available even when a store is closed
        [CheckAccessClosedStore(true)]
        //available even when navigation is not allowed
        [CheckAccessPublicStore(true)]
        //ignore SEO friendly URLs checks
        [CheckLanguageSeoCode(true)]
        public virtual async Task<IActionResult> RobotsTextFile()
        {
            var robotsFileContent = await _commonModelFactory.PrepareRobotsTextFileAsync();
            
            return Content(robotsFileContent, MimeTypes.TextPlain);
        }

        public virtual IActionResult GenericUrl()
        {
            //seems that no entity was found
            return InvokeHttp404();
        }

        //store is closed
        //available even when a store is closed
        [CheckAccessClosedStore(true)]
        public virtual IActionResult StoreClosed()
        {
            return View();
        }

        //helper method to redirect users. Workaround for GenericPathRoute class where we're not allowed to do it
        public virtual IActionResult InternalRedirect(string url, bool permanentRedirect)
        {
            //ensure it's invoked from our GenericPathRoute class
            if (HttpContext.Items["nop.RedirectFromGenericPathRoute"] == null ||
                !Convert.ToBoolean(HttpContext.Items["nop.RedirectFromGenericPathRoute"]))
            {
                url = Url.RouteUrl("Homepage");
                permanentRedirect = false;
            }

            //home page
            if (string.IsNullOrEmpty(url))
            {
                url = Url.RouteUrl("Homepage");
                permanentRedirect = false;
            }

            //prevent open redirection attack
            if (!Url.IsLocalUrl(url))
            {
                url = Url.RouteUrl("Homepage");
                permanentRedirect = false;
            }

            if (permanentRedirect)
                return RedirectPermanent(url);

            return Redirect(url);
        }

        #endregion
    }
}