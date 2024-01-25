using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core;
using Nop.Plugin.Misc.EtsyToNopcommerce.Models;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using System.Net;
using RestSharp;
using Nop.Plugin.Misc.EtsyToNopcommerce.Models.Shops;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Nop.Plugin.Misc.EtsyToNopcommerce.Models.Etsy.Listings;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Services.Catalog;
using Nop.Services.Customers;
using StackExchange.Profiling.Internal;
using Nop.Services.Directory;
using Nop.Data;
using Nop.Services.Common;
using StateProvince = Nop.Core.Domain.Directory.StateProvince;
using System.IO;
using DocumentFormat.OpenXml.Presentation;
using ImageProcessor;
using ImageProcessor.Plugins.WebP.Imaging.Formats;
using LinqToDB.Common;
using Nop.Plugin.Misc.EtsyToNopcommerce.Domains;
using Nop.Plugin.Misc.EtsyToNopcommerce.Services;
using Nop.Services.Media;
using Picture = Nop.Core.Domain.Media.Picture;
using Nop.Core.Domain.Catalog;
using Nop.Services.Seo;
using Nop.Web.Factories;
using Nop.Web.Models.Customer;
using Nop.Plugin.Misc.EtsyToNopcommerce.Models.Etsy;


namespace Nop.Plugin.Misc.EtsyToNopcommerce.Controllers
{

    [Area(AreaNames.Admin)]
    [AutoValidateAntiforgeryToken]
    public class EtsyToNopcommerceController : BasePluginController
    {
        #region Fields

        private readonly ILocalizationService _localizationService;
        private readonly INotificationService _notificationService;
        private readonly IPermissionService _permissionService;
        private readonly ISettingService _settingService;
        private readonly IStoreContext _storeContext;
        private readonly ICustomerService _customerService;
        private readonly IProductService _productService;
        private readonly ICountryService _countryService;
        private readonly IAddressService _addressService;
        private readonly IRepository<StateProvince> _stateProvinceRepository;
        private readonly IRepository<Address> _addressRepository;
        private readonly IPictureService _pictureService;
        private readonly ICustomProductReviewMappingService _customProductReviewMappingService;
        private readonly CatalogSettings _catalogSettings;
        private readonly IBackgroundQueue _queue;
        private readonly IUrlRecordService _urlRecordService;
        private readonly ICustomerRegistrationService _customerRegistrationService;
        private readonly ICustomerModelFactory _customerModelFactory;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly CustomerSettings _customerSettings;
        private readonly IProductReviewsEtsyReviewService _etsyReviewService;
        private readonly IEtsyListingsService _etsyListingsService;
        private readonly IProductReviewsTransactionsMappingService _productReviewsTransactionsMappingService;
        #endregion

        #region Ctor

        public EtsyToNopcommerceController(ILocalizationService localizationService,
            INotificationService notificationService,
            IPermissionService permissionService,
            ISettingService settingService,
            IStoreContext storeContext,
           ICustomerService customerService,IProductService productService,ICountryService countryService, IAddressService addressService,
            IRepository<StateProvince> stateProvinceRepository, IRepository<Address> addressRepository, IPictureService pictureService,
            ICustomProductReviewMappingService customProductReviewMappingService, CatalogSettings catalogSettings, IBackgroundQueue queue,
            IUrlRecordService urlRecordService, ICustomerRegistrationService customerRegistrationService, ICustomerModelFactory customerModelFactory,
            IGenericAttributeService genericAttributeService, CustomerSettings customerSettings, IProductReviewsEtsyReviewService etsyReviewService, IProductReviewsTransactionsMappingService productReviewsTransactionsMappingService,
            IEtsyListingsService etsyListingsService)
        {
            _localizationService = localizationService;
            _notificationService = notificationService;
            _permissionService = permissionService;
            _settingService = settingService;
            _storeContext = storeContext;
            _productService= productService;
            _customerService = customerService;
            _countryService= countryService;
            _addressService= addressService;
            _stateProvinceRepository= stateProvinceRepository;
            _addressRepository= addressRepository;
            _pictureService= pictureService;
            _customProductReviewMappingService= customProductReviewMappingService;
            _catalogSettings= catalogSettings;
            _queue= queue;
            _urlRecordService = urlRecordService;
            _customerRegistrationService = customerRegistrationService;
            _customerModelFactory= customerModelFactory;
            _genericAttributeService= genericAttributeService;
            _customerSettings= customerSettings;
            _etsyReviewService=etsyReviewService;
            _productReviewsTransactionsMappingService=productReviewsTransactionsMappingService;
            _etsyListingsService=etsyListingsService;

        }

        #endregion

    

        #region Methods

        public async Task<IActionResult> Configure()
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.AccessAdminPanel))
                return AccessDeniedView();

            var storeId = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var settings = await _settingService.LoadSettingAsync<EtsyToNopcommerceSettings>(storeId);


            var model = new ConfigurationModel
            {
                ShopName = settings.ShopName,
                ShopId = settings.ShopId,
                RequestUrl = settings.RequestUrl,
                RequestAccessTokenUrl = settings.RequestAccessTokenUrl,
                ConsumerKey = settings.ConsumerKey,
                ConsumerSecret = settings.ConsumerSecret,
                Token = settings.Token,
                TokenSecret = settings.TokenSecret,
                RefreshToken = settings.RefreshToken,
                TokenDate = settings.TokenDate,
                ActiveStoreScopeConfiguration = storeId

            };

            //we don't need some of the shared settings that loaded above, so load them separately for chosen store
            if (storeId > 0)
            {
                model.ShopName_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.ShopName, storeId);
                model.ShopId_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.ShopId, storeId);
                model.RequestUrl_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.RequestUrl, storeId);
                model.RequestAccessTokenUrl_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.RequestAccessTokenUrl, storeId);
                model.ConsumerKey_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.ConsumerKey, storeId);
                model.ConsumerSecret_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.ConsumerSecret, storeId);
                model.RefreshToken_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.RefreshToken, storeId);
                model.TokenSecret_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.TokenSecret, storeId);
                model.TokenDate_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.TokenDate, storeId);
               
            }



            //ensure credentials are valid
            //if (!string.IsNullOrEmpty(settings.ClientId) && !string.IsNullOrEmpty(settings.SecretKey))
            //{
            //    var (_, credentialsError) = await _serviceManager.GetAccessTokenAsync(settings);
            //    if (!string.IsNullOrEmpty(credentialsError))
            //        _notificationService.ErrorNotification(await _localizationService.GetResourceAsync("Plugins.Payments.PayPalCommerce.Credentials.Invalid"));
            //    else
            //        _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Plugins.Payments.PayPalCommerce.Credentials.Valid"));
            //}

            if (model.RefreshToken.IsNullOrEmpty())
            {
                _notificationService.WarningNotification(await _localizationService.GetResourceAsync("Nop.Plugins.Misc.EtsyToNopcommerce.Fields.AuthorizeNecessary"));
            }
           

            return View("~/Plugins/Nop.Plugin.Misc.EtsyToNopcommerce/Views/Configure.cshtml", model);
         
        }

        [HttpPost, ActionName("Configure")]
        [FormValueRequired("save")]
        public async Task<IActionResult> Configure(ConfigurationModel model)
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            var storeId = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var settings = await _settingService.LoadSettingAsync<EtsyToNopcommerceSettings>(storeId);

            //set new settings values
            settings.RequestAccessTokenUrl = model.RequestAccessTokenUrl;
            settings.ConsumerKey = model.ConsumerKey;
            settings.ConsumerSecret = model.ConsumerSecret;
            settings.Token = model.Token;
            settings.TokenSecret = model.TokenSecret;
            settings.RefreshToken = model.RefreshToken;
            settings.ShopName=model.ShopName;
            settings.ShopId=model.ShopId;
            settings.RequestUrl=model.RequestUrl;
            settings.TokenDate=model.TokenDate;
            
      
            await _settingService.SaveSettingAsync(settings);
            await _settingService.ClearCacheAsync();

            _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

            return await Configure();
        }

        private string GenerateCodeChallenge(string codeVerifier)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(codeVerifier));
            var b64Hash = Convert.ToBase64String(hash);
            var code = Regex.Replace(b64Hash, "\\+", "-");
            code = Regex.Replace(code, "\\/", "_");
            code = Regex.Replace(code, "=+$", "");
            return code;
        }

        [HttpGet]
        public async Task<IActionResult> Yetkilendir()
        {
            var storeId = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var settings = await _settingService.LoadSettingAsync<EtsyToNopcommerceSettings>(storeId);

         
            if (settings != null)
            {
                string code_challenge = GenerateCodeChallenge(settings.ConsumerKey);

                //https://localhost:59857/etys-yetkilendir
                string callbackUrl = $"{this.Request.Scheme}://{this.Request.Host}" + "/Admin/EtsyToNopcommerce/EtsyYetkilendir";

                string url = $"{settings.RequestUrl}?response_type=code&redirect_uri={callbackUrl}&scope=address_r%20address_w%20billing_r%20cart_r%20cart_w%20email_r%20favorites_r%20favorites_w%20feedback_r%20listings_d%20listings_r%20listings_w%20profile_r%20profile_w%20recommend_r%20recommend_w%20shops_r%20shops_w%20transactions_r%20transactions_w&client_id={settings.ConsumerKey}&state=superstate&code_challenge={code_challenge}&code_challenge_method=S256";
                return Redirect(url);
            }
            else
            {
               
                return await Configure();
            }
        }



        [HttpGet]
        public async Task<IActionResult> EtsyYetkilendir()
        {
            var storeId = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var settings = await _settingService.LoadSettingAsync<EtsyToNopcommerceSettings>(storeId);

            if (settings != null)
            {
                string RequestAccessTokenUrl = settings.RequestAccessTokenUrl;
                string ConsumerKey = settings.ConsumerKey;
                string ConsumerSecret = settings.ConsumerSecret;
                string TokenSecret = "";
                // Read token and verifier
                string code = Request.Query["code"];


                string redirect_uri = $"{this.Request.Scheme}://{this.Request.Host}" + "/Admin/EtsyToNopcommerce/EtsyYetkilendir";
                var  RestClient = new RestSharp.RestClient("https://openapi.etsy.com");
                var request = new RestRequest("/v3/public/oauth/token",Method.POST);
                request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
                request.AddParameter("grant_type", "authorization_code");
                request.AddParameter("client_id", ConsumerKey);
                request.AddParameter("redirect_uri", redirect_uri);
                request.AddParameter("code", code);
                request.AddParameter("code_verifier", ConsumerKey);

                var response = await RestClient.ExecutePostTaskAsync(request);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var AuthorizationResponse = Newtonsoft.Json.JsonConvert.DeserializeObject<AuthorizationResponse>(response.Content);
                    settings.Token = AuthorizationResponse.access_token;
                    settings.RefreshToken = AuthorizationResponse.refresh_token;
                    settings.ExpiresIn = AuthorizationResponse.expires_in;
                    settings.TokenDate = DateTime.Now;




                    //Shop id al
                    redirect_uri = $"{this.Request.Scheme}://{this.Request.Host}" + "/Admin/EtsyToNopcommerce/EtsyYetkilendir";
                    RestClient RestClient1 = new RestSharp.RestClient("https://openapi.etsy.com");
                    RestRequest request1 = new RestRequest("/v3/application/shops");



                    request1.AddHeader("Content-Type", "application/x-www-form-urlencoded");
                    request1.AddHeader("x-api-key", "uh3pwbu285jcynbsz50cadww");
                    request1.AddHeader("Authorization", "Bearer " + settings.Token);
                    request1.AddParameter("shop_name", settings.ShopName);

                    var response1 = await RestClient1.ExecuteGetTaskAsync(request1);
                    string content = response1.Content;
                    settings.ShopId = Newtonsoft.Json.JsonConvert.DeserializeObject<Shops>(content).results.First()
                        .shop_id;


                    //ayarları sakla
                    await _settingService.SaveSettingAsync(settings);
                    await _settingService.ClearCacheAsync();

                    _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Nop.Plugins.Misc.EtsyToNopcommerce.Fields.AuthorizeSuccess"));

                    return await Configure();
                }
                else
                {
                    return await Configure();
                }
                return await Configure();
            }
            else
            {
                return await Configure();
            }
        }

 [HttpGet]
        public async Task<IActionResult> EtsyYorumlariniCagir()
        {
            var storeId = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var settings = await _settingService.LoadSettingAsync<EtsyToNopcommerceSettings>(storeId);

            if (settings != null)
            {
                _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));
                //Todo:buraya servisler ile istek ekle
                string reviewResult=  await ReviewlariAlVeIsle();

              if (reviewResult != null)
              {
                    _notificationService.SuccessNotification(reviewResult.Split(",")[0] +" adet yorum, "+ reviewResult.Split(",")[1] +" adet fotograflı yorum eklendi");
                }
                
               
                return await Configure();
            }
            else
            {
                return await Configure();
            }
        }

        public async Task<IRestResponse> EtsyRequests(string url,List<KeyValuePair<string, string>> parameters)
        {
            Thread.Sleep(250);
            var storeId = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var settings = await _settingService.LoadSettingAsync<EtsyToNopcommerceSettings>(storeId);

            string BaseAdres = "https://openapi.etsy.com/";
           


            RestClient restClient = new RestSharp.RestClient("https://openapi.etsy.com");
            RestRequest request = new RestRequest(url);
            IRestResponse response = null;
            request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
                if (url != "v3/public/oauth/token")
                {
                    request.RequestFormat = DataFormat.Json;
                    request.AddHeader("x-api-key", settings.ConsumerKey);
                    request.AddHeader("Authorization", $"Bearer {settings.Token}");
                    foreach (var parameter in parameters)
                    {
                        request.AddParameter(parameter.Key, parameter.Value);


                    }
                    response = await restClient.ExecuteGetTaskAsync(request);
                }
                else
                {
                    //var content = new FormUrlEncodedContent(parameters);
                    foreach (var parameter in parameters)
                    {
                        request.AddParameter(parameter.Key, parameter.Value, ParameterType.GetOrPost);
                       

                }
                     response = await restClient.ExecutePostTaskAsync(request);
                }








                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {

                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    //Hata log
                    //_loglist.LogCritical("OtomatikRefreshHatasi: " + "/" + response.Content + " at {DT}",
                    //    DateTime.UtcNow.ToLongTimeString());
                }
                else
                {
                    //Hata log
                    //_loglist.LogCritical(response.Content + " at {DT}",
                    //    DateTime.UtcNow.ToLongTimeString());
                }
            



            return response;
        }
        public static async Task<List<T>> WhenAllEx<T>(List<Task<T>> tasks)
        {
            // get Task which completes when all 'tasks' have completed
            List<string> sonuc = new List<string>();
            var whenAllTask = Task.WhenAll(tasks);
            for (; ; )
            {
                // get Task which completes after 250ms
                var timer = Task.Delay(350); // you might want to make this configurable
                // Wait until either all tasks have completed OR 250ms passed

                await Task.WhenAny(whenAllTask, timer);
              
                // if all tasks have completed, complete the returned task
                if (whenAllTask.IsCompleted)
                {
                    return whenAllTask.Result.ToList();
                }
                // Otherwise call progress report lambda and do another round

            }

        }
        private async Task CheckTokenExpire(bool authourised)
        {
            var storeId = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var ayar = await _settingService.LoadSettingAsync<EtsyToNopcommerceSettings>(storeId);

            if (ayar != null)
            {

                string requestGetTokenUrl = "v3/public/oauth/token";

                var suan = DateTime.Now;
                DateTime? tokenDate = ayar.TokenDate;
                ;
                if (ayar.TokenDate != null)
                {

                    var tokenExpiredate = tokenDate.Value.AddSeconds(ayar.ExpiresIn - 1000);
                    if (suan > tokenExpiredate || !authourised)
                    {
                        // Refresh Token Alınıyor.....
                        List<KeyValuePair<string, string>> parameters = new List<KeyValuePair<string, string>>();
                        parameters.Add(new KeyValuePair<string, string>("grant_type", "refresh_token"));
                        parameters.Add(new KeyValuePair<string, string>("client_id", ayar.ConsumerKey));
                        parameters.Add(new KeyValuePair<string, string>("refresh_token", ayar.RefreshToken));

                        var tokResponse = await EtsyRequests(requestGetTokenUrl, parameters);

                        if (tokResponse.StatusCode == System.Net.HttpStatusCode.OK)
                        {
                            var AuthorizationResponse =
                                Newtonsoft.Json.JsonConvert.DeserializeObject<AuthorizationResponse>(
                                    tokResponse.Content);
                            ayar.Token = AuthorizationResponse.access_token;
                            ayar.RefreshToken = AuthorizationResponse.refresh_token;
                            ayar.ExpiresIn = AuthorizationResponse.expires_in;
                            ayar.TokenDate = DateTime.Now;

                            await _settingService.SaveSettingAsync(ayar);
                            await _settingService.ClearCacheAsync();
                        }
                    }
                }
                else
                {
                    // Refresh Token Alınıyor.....
                    List<KeyValuePair<string, string>> parameters = new List<KeyValuePair<string, string>>();
                    parameters.Add(new KeyValuePair<string, string>("grant_type", "refresh_token"));
                    parameters.Add(new KeyValuePair<string, string>("client_id", ayar.ConsumerKey));
                    parameters.Add(new KeyValuePair<string, string>("refresh_token", ayar.RefreshToken));

                    var tokResponse = await EtsyRequests(requestGetTokenUrl, parameters);

                    if (tokResponse.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        var AuthorizationResponse =
                            Newtonsoft.Json.JsonConvert.DeserializeObject<AuthorizationResponse>(
                                tokResponse.Content);
                        ayar.Token = AuthorizationResponse.access_token;
                        ayar.RefreshToken = AuthorizationResponse.refresh_token;
                        ayar.ExpiresIn = AuthorizationResponse.expires_in;
                        ayar.TokenDate = DateTime.Now;

                        await _settingService.SaveSettingAsync(ayar);
                        await _settingService.ClearCacheAsync();
                    }
                }


            }


        }


        private async Task<HashSet<EtsyReview>?> GetEtsyReviews(string requestReviewsUrl, List<KeyValuePair<string, string>> getReviewsParameters)
        {
            IRestResponse getReviewsResponse;

            EtsyReviews getReviewsResult;
            getReviewsResponse =
                await EtsyRequests(requestReviewsUrl, getReviewsParameters);

            if (getReviewsResponse.StatusCode == HttpStatusCode.OK)
            {
                try
                {
                    getReviewsResult =
                        Newtonsoft.Json.JsonConvert
                            .DeserializeObject<EtsyReviews>(
                                getReviewsResponse.Content.Replace("&amp;", "&").Replace("Etsy","web site"));
                 var newResult = new HashSet<EtsyReview>();
             

                    foreach (var result in getReviewsResult.Results)
                    {
                        result.Review= System.Web.HttpUtility.HtmlDecode(result.Review);
                        newResult.Add(result);
                    }
                    return newResult;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }

            return null;
        }


      
        private async Task<Transaction> GetTransactions(string requestTransactionsUrl, List<KeyValuePair<string, string>> getListingsParameters)
        {
            IRestResponse getEtsyTransactionResponse;

            Transaction getEtsyTransactionsResult;
            getEtsyTransactionResponse =
                await EtsyRequests(requestTransactionsUrl, getListingsParameters);

            if (getEtsyTransactionResponse.StatusCode == HttpStatusCode.OK)
            {
                try
                {
                    getEtsyTransactionsResult =
                        Newtonsoft.Json.JsonConvert
                            .DeserializeObject<Transaction>(
                                getEtsyTransactionResponse.Content.Replace("&amp;", "&"));
                    return getEtsyTransactionsResult;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }

            return null;
        }


        private async Task<Receipt> GetReceipts(string requestReceiptsUrl, List<KeyValuePair<string, string>> getListingsParameters)
        {
            IRestResponse getEtsyReceiptsResponse;

            Receipt getEtsyReceiptsResult;
            getEtsyReceiptsResponse =
                await EtsyRequests(requestReceiptsUrl, getListingsParameters);

            if (getEtsyReceiptsResponse.StatusCode == HttpStatusCode.OK)
            {
                try
                {
                    getEtsyReceiptsResult =
                        Newtonsoft.Json.JsonConvert
                            .DeserializeObject<Receipt>(
                                getEtsyReceiptsResponse.Content.Replace("&amp;", "&"));
                    return getEtsyReceiptsResult;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }

            return null;
        }

        public async Task<string> InsertReviewMedia(string ProductSeName, UploadDataBinary data, int reviewId)
        {

            string name = ProductSeName + "-" + DateTime.UtcNow.ToFileTime();
            Stopwatch sw = new Stopwatch();

            var pic = new Picture();


            var vid = new Video();
            if (data.Extentions.Contains("image"))
            {
                try
                {
                    sw.Start();

                    var ms = new MemoryStream();
                    ImageFactory imageFactory = new ImageFactory(preserveExifData: false);
                    imageFactory.Load(data.BinaryData).Format(new WebPFormat()).Quality(90).Save(ms);
                    sw.Stop();
                    Console.WriteLine("Elapsed Picture Encode={0}", sw.Elapsed);

                    byte[] raw = ms.ToArray();
                    await ms.DisposeAsync();
                    imageFactory.Dispose();


                    pic = await _pictureService.InsertPictureAsync(raw, "image/webp", name);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message + Environment.NewLine);
                }
            }
            //else if (data.Extentions.Contains("video"))
            //{
            //    try
            //    {
            //        vid = await _videoService.InsertVideoAsync(data.BinaryData, name, data.Extentions);
            //    }
            //    catch (Exception e)
            //    {
            //        System.IO.File.AppendAllText(@"customProductReview.log", e.InnerException + Environment.NewLine);
            //    }
            //}

            int? lastPicId = pic.Id;
            int? lastVidId = 0;
            if (lastPicId == 0)
            {
                lastPicId = null;
            }

            if (lastVidId == 0)
            {
                lastVidId = null;
            }


            if (!(lastPicId == null && lastVidId == null))
            {
                await _customProductReviewMappingService.InsertCustomProductReviewMappingAsync(reviewId, lastPicId,
                    lastVidId);
            }

            return "done";
        }



        //Etsy yorumlarını Nopcommerce websitesine çekme planı

        //1- Api bilgilerini gir mağaza bilgileri için Uygulamaya Etsy izin ver+

        //2-Mağaza adını çek mağaza adından mağaza id sini bul+

        //3- reviewları çek
        //https://openapi.etsy.com/v3/application/shops/21815852/reviews



        //6-ilgili review'a ait translaction id ile receipt id bul ayrıca sku burda
        //    https://openapi.etsy.com/v3/application/shops/21815852/transactions/3561732827


        //7-İlgili Receipt id ile receipt detaylarına gir ve alıcı bilgilerini bul
        //    https://openapi.etsy.com/v3/application/shops/21815852/receipts/2888678949

        //8- bu bilgilerle nopcommerce de yeni kullanıcı oluştur ve ilgili sku ile eşleşen listeye yorum ekle

        private async Task<string> ReviewlariAlVeIsle()
        {
            var storeId = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var currentStore = await _storeContext.GetCurrentStoreAsync();
            storeId= currentStore.Id;
            var ayar = await _settingService.LoadSettingAsync<EtsyToNopcommerceSettings>(storeId);
            string BaseAdres = "https://openapi.etsy.com/";

            string requestGetShopReviewsUrl = $"v3/application/shops/{ayar.ShopId}/reviews";


            int reviewsAdded = 0;
            int reviewsWithPhotoAdded = 0;


            await CheckTokenExpire(true);
            await _settingService.ClearCacheAsync();
            ayar = await _settingService.LoadSettingAsync<EtsyToNopcommerceSettings>(storeId);

            #region Etsy Reviewları Çagırma Bölümü
            List<KeyValuePair<string, string>> getReviewsParameters = new List<KeyValuePair<string, string>>
                        {
                            new KeyValuePair<string, string>("limit", "1")
                        };
            var getShopReviewsResponse = await EtsyRequests(requestGetShopReviewsUrl, getReviewsParameters);

            if (getShopReviewsResponse.StatusCode == System.Net.HttpStatusCode.OK)
            {
                var getShopReviewsResult =
                    Newtonsoft.Json.JsonConvert.DeserializeObject<EtsyReviews>(
                        getShopReviewsResponse.Content.Replace("&amp;", "&"));

                if (getShopReviewsResult.Count > 0)
                {
                    int maxcount = int.Parse(getShopReviewsResult.Count.ToString());
                    double sirasayisi = 0;
                    double x = (double)maxcount / (double)100;
                    sirasayisi = Math.Round(x, 0);
                    if (sirasayisi < 1)
                    {
                        sirasayisi = 1;
                    }

                    List<Task<HashSet<EtsyReview>?>> getReviewssJobs = new List<Task<HashSet<EtsyReview>>>();

                    for (int i = 0; i < sirasayisi + 2; i++)
                    {
                        getReviewsParameters.Clear();
                        getReviewsParameters.Add(new KeyValuePair<string, string>("limit", "100"));
                        getReviewsParameters.Add(new KeyValuePair<string, string>("offset", (i * 100).ToString()));
                        getReviewssJobs.Add(GetEtsyReviews(requestGetShopReviewsUrl, getReviewsParameters));
                    }

                   var reviewsList= await WhenAllEx(getReviewssJobs);
                   var EtsyReviewList = new HashSet<EtsyReview>();

                   foreach (var reviewResults in reviewsList)
                   {
                       foreach (var review in reviewResults)
                       {
                           if (review!=null &&review.TransactionId!=null)
                           {
                               EtsyReviewList.Add(review);
                               
                               var reviewVarmi =await _etsyReviewService.AskEtsyReviewByTransactionIdAsync(review.TransactionId.Value);
                               if (!reviewVarmi)
                               {
                                var result=  await _etsyReviewService.InsertEtsyReviewAsync(review.ShopId, review.TransactionId.Value,
                                       review.ListingId, review.BuyerUserId,
                                       review.Rating, review.Review, review.Language, review.ImageUrlFullxfull,
                                       review.CreateTimestamp, review.CreatedTimestamp,
                                       review.UpdateTimestamp, review.UpdatedTimestamp,"");
                               }

                           }
                          
                       }
                   }

                   //Todo:Buraları servise çevir

                   HashSet<long?> listingIds =
                       EtsyReviewList.Select(x => x.ListingId).Distinct().ToHashSet();

                   HashSet<long?> translactionIds =
                       EtsyReviewList.Select(x => x.TransactionId).Distinct().ToHashSet();

                   



                 
                   

                    List<Task<Transaction>> getTransactionJob = new List<Task<Transaction>>();

                    foreach (var translactionId in translactionIds)
                    {
                        string requestTranlactionsUrl = $"v3/application/shops/{ayar.ShopId}/transactions/"+ translactionId;
                        getTransactionJob.Add( GetTransactions(requestTranlactionsUrl, new List<KeyValuePair<string, string>>()));
                    }
                    //review'ı olan ürünlerle ilgili receipt_id ve skular burda 
                    var etsyTransactionsList = await WhenAllEx(getTransactionJob);
                    etsyTransactionsList = etsyTransactionsList.Where(x => x != null).ToList();
                    var etsyyy = EtsyReviewList.Where(x =>
                        etsyTransactionsList.Any(y => y.TransactionId == x.TransactionId));

                    EtsyReviewList = etsyyy.ToHashSet();
                    HashSet<long?> receiptIds =
                        etsyTransactionsList.Select(x => x.ReceiptId).Distinct().ToHashSet();


                    List<Task<Receipt>> getReceiptsJob = new List<Task<Receipt>>();
                    foreach (var receiptId in receiptIds)
                    {
                        string requestReceiptUrl = $"v3/application/shops/{ayar.ShopId}/receipts/" + receiptId;
                        getReceiptsJob.Add( GetReceipts(requestReceiptUrl, new List<KeyValuePair<string, string>>()));
                    }

                    //review'ı olan ürünlerle ilgili adresler burda
                    List<Receipt> etsyReceiptsList = await WhenAllEx(getReceiptsJob);
                    etsyReceiptsList = etsyReceiptsList.Where(x => x != null).ToList();

                 
                    //tüm datalar elimizde şimdi yorumları sırası ile ilgili ilana yükleme
                    foreach (var etsyReview in EtsyReviewList)
                    {
                        //ilk olarak ilgili yorum hangi ürüne ait skusunu bulup siteden ilgili ürünü bulmak lazım
                        try
                        {

                       
                        var ilgliTransaction=etsyTransactionsList.Single(x => x.TransactionId == etsyReview.TransactionId);
                        var ilgiliSku = ilgliTransaction.Sku;
                        var ilgiliReceipt = etsyReceiptsList.Single(x => x.ReceiptId == ilgliTransaction.ReceiptId);

                        var ilgiliNopcommerceUrun=await _productService.GetProductBySkuAsync(ilgiliSku);

                        if (ilgiliNopcommerceUrun != null)
                        {
                            //bu yorum bu ürün için daha önce yapılmışmı kontrol et
                            var productReviewsByproductId = await _productService.GetAllProductReviewsAsync(productId: ilgiliNopcommerceUrun.Id);
                            if (productReviewsByproductId!=null)
                            {
                                var buYorumDahaOnceYapildiMi = productReviewsByproductId.ToList().Any(x => x.ReviewText.Contains(etsyReview.Review));
                                if (!buYorumDahaOnceYapildiMi)
                                {

                                    var buMusteriDahaOnceKayitOlduMu = (await _customerService.GetAllCustomersAsync(email: ilgiliReceipt.BuyerEmail)).ToList()
                                        .Any(x => x.Email.Contains(ilgiliReceipt.BuyerEmail));
                                    Random rnd = new Random();
                                    //eğer müşteri maili daha önce kayıt edilmediyse yeni müşteri kadı oluşturulacak
                                    if (!buMusteriDahaOnceKayitOlduMu)
                                    {
                                        var registerModel = new RegisterModel();
                                        registerModel = await _customerModelFactory.PrepareRegisterModelAsync(registerModel, false, setDefaultValues: true);






                                        Customer newEtsyCustomer = new Customer();



                                        var yorumTarihi = int.Parse(etsyReview.CreatedTimestamp.ToString())
                                            .UnixTimeStampToDateTime().AddDays(-rnd.Next(30, 600));
                                        var musteriGeneratedEmail
                                            = rnd.Next(1000, 9999) + "_" + ilgiliReceipt.BuyerEmail;
                                        newEtsyCustomer.Email = musteriGeneratedEmail;
                                        newEtsyCustomer.Active = true;
                                        newEtsyCustomer.CreatedOnUtc = yorumTarihi;
                                        newEtsyCustomer.AdminComment = "EtsyToNopcommerceGeneratedCustomer";
                                        newEtsyCustomer.LastActivityDateUtc = yorumTarihi.AddDays(rnd.Next(3, 600));
                                        newEtsyCustomer.LastLoginDateUtc = yorumTarihi.AddDays(rnd.Next(3, 600));
                                        newEtsyCustomer.RegisteredInStoreId = storeId;


                                        await _customerService.InsertCustomerAsync(newEtsyCustomer);

                                        var kayitliMusteri = await _customerService.GetCustomerByEmailAsync(musteriGeneratedEmail);


                                        Address yeniMusteriAdresiAddress = new Address();

                                        string firstName = "";
                                        string lastName = "";
                                        if (ilgiliReceipt.Name.Contains(" "))
                                        {
                                            firstName = ilgiliReceipt.Name.Split(" ")[0];
                                            lastName = ilgiliReceipt.Name.Split(" ")[1];
                                        }
                                        else
                                        {
                                            firstName = ilgiliReceipt.Name;
                                        }

                                        yeniMusteriAdresiAddress.FirstName = firstName;
                                        yeniMusteriAdresiAddress.LastName = lastName;
                                        yeniMusteriAdresiAddress.Email = musteriGeneratedEmail;
                                        yeniMusteriAdresiAddress.CreatedOnUtc = yorumTarihi;
                                        yeniMusteriAdresiAddress.Address1 = ilgiliReceipt.FirstLine;
                                        yeniMusteriAdresiAddress.Address2 = ilgiliReceipt.SecondLine;
                                        yeniMusteriAdresiAddress.City = ilgiliReceipt.City;
                                        yeniMusteriAdresiAddress.ZipPostalCode = ilgiliReceipt.Zip;


                                        var ilgiliCountry = await _countryService.GetCountryByTwoLetterIsoCodeAsync(ilgiliReceipt.CountryIso);
                                        var ilgiliStateVarmi = await _stateProvinceRepository.Table.AnyAsync(x =>
                                            x.Name.ToLower() == ilgiliReceipt.State.ToLower() && x.CountryId == ilgiliCountry.Id);
                                        bool ilgliStateKisaltilmismi = false;
                                        if (!ilgiliStateVarmi)
                                        {
                                            ilgiliStateVarmi = await _stateProvinceRepository.Table.AnyAsync(x => x.Abbreviation == ilgiliReceipt.State && x.CountryId == ilgiliCountry.Id);
                                            if (ilgiliStateVarmi)
                                            {
                                                ilgliStateKisaltilmismi = true;

                                            }
                                        }


                                        if (ilgiliStateVarmi)
                                        {
                                            if (ilgliStateKisaltilmismi)
                                            {
                                                var ilgiliState = await _stateProvinceRepository.Table.SingleAsync(x => x.Abbreviation == ilgiliReceipt.State && x.CountryId == ilgiliCountry.Id);
                                                yeniMusteriAdresiAddress.StateProvinceId = ilgiliState.Id;
                                            }
                                            else
                                            {
                                                var ilgiliState = await _stateProvinceRepository.Table.SingleAsync(x =>
                                                    x.Name.ToLower() == ilgiliReceipt.State.ToLower() && x.CountryId == ilgiliCountry.Id);
                                                yeniMusteriAdresiAddress.StateProvinceId = ilgiliState.Id;
                                            }

                                        }


                                        yeniMusteriAdresiAddress.CountryId = ilgiliCountry.Id;
                                        await _addressService.InsertAddressAsync(yeniMusteriAdresiAddress);

                                        var kayitliMusteriAdresi = await _addressRepository.Table.SingleAsync(x => x.Email == musteriGeneratedEmail);
                                        await _customerService.InsertCustomerAddressAsync(kayitliMusteri, kayitliMusteriAdresi);

                                        kayitliMusteri.Active = true;
                                        kayitliMusteri.Username = kayitliMusteri.Email;
                                        kayitliMusteri.BillingAddressId = kayitliMusteriAdresi.Id;
                                        kayitliMusteri.ShippingAddressId = kayitliMusteriAdresi.Id;

                                        await _customerService.UpdateCustomerAsync(kayitliMusteri);

                                        //form fields


                                        if (_customerSettings.FirstNameEnabled)
                                            await _genericAttributeService.SaveAttributeAsync(kayitliMusteri, NopCustomerDefaults.FirstNameAttribute, kayitliMusteriAdresi.FirstName);
                                        if (_customerSettings.LastNameEnabled)
                                            await _genericAttributeService.SaveAttributeAsync(kayitliMusteri, NopCustomerDefaults.LastNameAttribute, kayitliMusteriAdresi.LastName);


                                    }

                                    var currentCustomer = (await _customerService.GetAllCustomersAsync(email: ilgiliReceipt.BuyerEmail)).ToList()
                                        .Single(x => x.Email.Contains(ilgiliReceipt.BuyerEmail));

                                    //yorum ekleme burada yapılacak

                                    var rating = etsyReview.Rating.Value;
                                    if (rating < 1 || rating > 5)
                                        rating = _catalogSettings.DefaultProductRatingValue;
                                    var isApproved = !_catalogSettings.ProductReviewsMustBeApproved;
                                    var customer = currentCustomer;

                                    var productReview = new ProductReview
                                    {
                                        ProductId = ilgiliNopcommerceUrun.Id,
                                        CustomerId = customer.Id,
                                        Title = "",
                                        ReviewText = etsyReview.Review,
                                        Rating = rating,
                                        HelpfulYesTotal = rnd.Next(1, 7),
                                        HelpfulNoTotal = 0,
                                        IsApproved = true,
                                        CreatedOnUtc = int.Parse(etsyReview.CreatedTimestamp.ToString())
                                            .UnixTimeStampToDateTime(),
                                        StoreId = storeId,
                                    };
                                    await _productService.InsertProductReviewAsync(productReview);
                                    reviewsAdded += 1;
                                    Console.WriteLine("Toplam " + reviewsAdded + " adet yorum eklendi");
                                    var reviewId = productReview.Id;




                                    //update product totals
                                    await _productService.UpdateProductReviewTotalsAsync(ilgiliNopcommerceUrun);



                                    #region Product Review Media Upload Section

                                    try
                                    {


                                        //pictures
                                        List<UploadDataBinary> dataList = new List<UploadDataBinary>();

                                        if (etsyReview.ImageUrlFullxfull != null)
                                        {
                                            WebClient client = new WebClient();

                                            byte[] bytes = await client.DownloadDataTaskAsync(etsyReview.ImageUrlFullxfull);

                                            var uploadData = new UploadDataBinary();
                                            uploadData.BinaryData = bytes;
                                            uploadData.Extentions = "image";

                                            dataList.Add(uploadData);

                                            //string fileName = "tempUpload"+DateTime.UtcNow.ToFileTime() + fileInfo.Extension;

                                            var productSeName = await _urlRecordService.GetSeNameAsync(ilgiliNopcommerceUrun, currentStore.DefaultLanguageId);
                                            foreach (var data in dataList)
                                            {
                                                _queue.QueueTask(async token =>
                                                {
                                                    reviewsWithPhotoAdded += 1;

                                                    Console.WriteLine("Toplam " + reviewsWithPhotoAdded + " adet fotograflı yorum eklendi");
                                                    await InsertReviewMedia(productSeName, data, reviewId);
                                                });
                                            }
                                        }


                                    }
                                    catch (Exception e)
                                    {
                                        Console.WriteLine(e);

                                    }
                                    #endregion

                                }
                            }
                          
                        }
                        else
                        {
                                Console.WriteLine("Error Message: Sku Number:" +ilgiliSku+" product is not found in website");
                        }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Error Message:"+e+"/"+e.Message);
                           
                        }

                    }
                    Console.WriteLine("Toplam "+reviewsAdded+" adet yorum eklendi");
                    Console.WriteLine("Toplam " + reviewsWithPhotoAdded + " adet fotograflı yorum eklendi");
                }
                else
                {

                }
            }
            else
            {
                await CheckTokenExpire(false);
            }

            #endregion


            return reviewsAdded.ToString() + "," + reviewsWithPhotoAdded.ToString();
        }


        #endregion
    }
}