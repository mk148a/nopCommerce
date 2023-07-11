using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.EMMA;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Misc.EtsyToNopcommerce;
using Nop.Plugin.Misc.EtsyToNopcommerce.Models;
using Nop.Services;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;
using System.Net;
using RestSharp;
using Nop.Plugin.Misc.EtsyToNopcommerce.Models.Shops;
using System.Collections.Generic;
using System.Threading;
using DocumentFormat.OpenXml.Bibliography;
using Nop.Plugin.Misc.EtsyToNopcommerce.Models.Reviews;
using Nop.Plugin.Misc.EtsyToNopcommerce.Models.Listings;
using Nop.Plugin.Misc.EtsyToNopcommerce.Models.Transactions;
using System.Security.Policy;
using Nop.Plugin.Misc.EtsyToNopcommerce.Models.Receipts;
using Nop.Services.Catalog;
using Nop.Services.Customers;
using Nop.Services.Logging;
using StackExchange.Profiling.Internal;

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
 

        #endregion

        #region Ctor

        public EtsyToNopcommerceController(ILocalizationService localizationService,
            INotificationService notificationService,
            IPermissionService permissionService,
            ISettingService settingService,
            IStoreContext storeContext,
           ICustomerService customerService,IProductService productService)
        {
            _localizationService = localizationService;
            _notificationService = notificationService;
            _permissionService = permissionService;
            _settingService = settingService;
            _storeContext = storeContext;
            _productService= productService;
            _customerService = customerService;
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
            
            //settings.PaymentType = (PaymentType)model.PaymentTypeId;
            //settings.DisplayButtonsOnShoppingCart = model.DisplayButtonsOnShoppingCart;
            //settings.DisplayButtonsOnProductDetails = model.DisplayButtonsOnProductDetails;
            //settings.DisplayLogoInHeaderLinks = model.DisplayLogoInHeaderLinks;
            //settings.LogoInHeaderLinks = model.LogoInHeaderLinks;
            //settings.DisplayLogoInFooter = model.DisplayLogoInFooter;
            //settings.DisplayPayLaterMessages = model.DisplayPayLaterMessages;
            //settings.LogoInFooter = model.LogoInFooter;

         

            //await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.SetCredentialsManually, model.SetCredentialsManually_OverrideForStore, storeId, false);
            //await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.PaymentType, model.PaymentTypeId_OverrideForStore, storeId, false);
            //await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.DisplayButtonsOnShoppingCart, model.DisplayButtonsOnShoppingCart_OverrideForStore, storeId, false);
            //await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.DisplayButtonsOnProductDetails, model.DisplayButtonsOnProductDetails_OverrideForStore, storeId, false);
            //await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.DisplayLogoInHeaderLinks, model.DisplayLogoInHeaderLinks_OverrideForStore, storeId, false);
            //await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.LogoInHeaderLinks, model.LogoInHeaderLinks_OverrideForStore, storeId, false);
            //await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.DisplayLogoInFooter, model.DisplayLogoInFooter_OverrideForStore, storeId, false);
            //await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.DisplayPayLaterMessages, model.DisplayPayLaterMessages_OverrideForStore, storeId, false);
            //await _settingService.SaveSettingOverridablePerStoreAsync(settings, setting => setting.LogoInFooter, model.LogoInFooter_OverrideForStore, storeId, false);
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

                    _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

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
                await ReviewlariAlVeIsle();
                _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));
               
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
                var tokenExpiredate = ayar.TokenDate.AddSeconds(ayar.ExpiresIn - 1000);
                if (suan > tokenExpiredate||!authourised)
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


        private async Task<HashSet<Models.Reviews.Result>?> GetEtsyReviews(string requestReviewsUrl, List<KeyValuePair<string, string>> getReviewsParameters)
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
                                getReviewsResponse.Content.Replace("&amp;", "&"));
                    return getReviewsResult.Results;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }

            return null;
        }

        private async Task<HashSet<Models.Listings.Result>?> GetEtsyListings(string requestListingsUrl, List<KeyValuePair<string, string>> getListingsParameters)
        {
            IRestResponse getEtsyListingsResponse;

            GetListingsById getEtsyListingsResult;
            getEtsyListingsResponse =
                await EtsyRequests(requestListingsUrl, getListingsParameters);

            if (getEtsyListingsResponse.StatusCode == HttpStatusCode.OK)
            {
                try
                {
                    getEtsyListingsResult =
                        Newtonsoft.Json.JsonConvert
                            .DeserializeObject<GetListingsById>(
                                getEtsyListingsResponse.Content.Replace("&amp;", "&"));
                    return getEtsyListingsResult.Results.ToHashSet();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }

            return null;
        }

      
        private async Task<Models.Transactions.Transactions> GetTransactions(string requestTransactionsUrl, List<KeyValuePair<string, string>> getListingsParameters)
        {
            IRestResponse getEtsyTransactionResponse;

            Transactions getEtsyTransactionsResult;
            getEtsyTransactionResponse =
                await EtsyRequests(requestTransactionsUrl, getListingsParameters);

            if (getEtsyTransactionResponse.StatusCode == HttpStatusCode.OK)
            {
                try
                {
                    getEtsyTransactionsResult =
                        Newtonsoft.Json.JsonConvert
                            .DeserializeObject<Transactions>(
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


        private async Task<Models.Receipts.Receipts> GetReceipts(string requestReceiptsUrl, List<KeyValuePair<string, string>> getListingsParameters)
        {
            IRestResponse getEtsyReceiptsResponse;

            Receipts getEtsyReceiptsResult;
            getEtsyReceiptsResponse =
                await EtsyRequests(requestReceiptsUrl, getListingsParameters);

            if (getEtsyReceiptsResponse.StatusCode == HttpStatusCode.OK)
            {
                try
                {
                    getEtsyReceiptsResult =
                        Newtonsoft.Json.JsonConvert
                            .DeserializeObject<Receipts>(
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

        private async Task ReviewlariAlVeIsle()
        {
            var storeId = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var ayar = await _settingService.LoadSettingAsync<EtsyToNopcommerceSettings>(storeId);
            string BaseAdres = "https://openapi.etsy.com/";

            string requestGetShopReviewsUrl = $"v3/application/shops/{ayar.ShopId}/reviews";



           

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

                    List<Task<HashSet<Models.Reviews.Result>?>> getReviewssJobs = new List<Task<HashSet<Models.Reviews.Result>>>();

                    for (int i = 0; i < sirasayisi + 2; i++)
                    {
                        getReviewsParameters.Clear();
                        getReviewsParameters.Add(new KeyValuePair<string, string>("limit", "100"));
                        getReviewsParameters.Add(new KeyValuePair<string, string>("offset", (i * 100).ToString()));
                        getReviewssJobs.Add(GetEtsyReviews(requestGetShopReviewsUrl, getReviewsParameters));
                    }

                   var reviewsList= await WhenAllEx(getReviewssJobs);
                   HashSet<Models.Reviews.Result> EtsyReviewList = new HashSet<Models.Reviews.Result>();

                   foreach (var reviewResults in reviewsList)
                   {
                       foreach (var review in reviewResults)
                       {
                           EtsyReviewList.Add(review);
                       }
                   }

                   HashSet<long?> listingIds =
                       EtsyReviewList.Select(x => x.ListingId).Distinct().ToHashSet();

                   HashSet<long?> translactionIds =
                       EtsyReviewList.Select(x => x.TransactionId).Distinct().ToHashSet();

                   



                 
                   

                    List<Task<Transactions>> getTransactionJob = new List<Task<Transactions>>();

                    foreach (var translactionId in translactionIds)
                    {
                        string requestTranlactionsUrl = $"v3/application/shops/{ayar.ShopId}/transactions/"+ translactionId;
                        getTransactionJob.Add( GetTransactions(requestTranlactionsUrl, new List<KeyValuePair<string, string>>()));
                    }
                    //review'ı olan ürünlerle ilgili receipt_id ve skular burda 
                    var etsyTransactionsList = await WhenAllEx(getTransactionJob);
                    etsyTransactionsList = etsyTransactionsList.Where(x => x != null).ToList();
                    HashSet<long?> receiptIds =
                        etsyTransactionsList.Select(x => x.ReceiptId).Distinct().ToHashSet();


                    List<Task<Receipts>> getReceiptsJob = new List<Task<Receipts>>();
                    foreach (var receiptId in receiptIds)
                    {
                        string requestReceiptUrl = $"v3/application/shops/{ayar.ShopId}/receipts/" + receiptId;
                        getReceiptsJob.Add( GetReceipts(requestReceiptUrl, new List<KeyValuePair<string, string>>()));
                    }

                    //review'ı olan ürünlerle ilgili adresler burda
                    List<Receipts> etsyReceiptsList = await WhenAllEx(getReceiptsJob);
                    etsyReceiptsList = etsyReceiptsList.Where(x => x != null).ToList();
                    //tüm datalar elimizde şimdi yorumları sırası ile ilgili ilana yükleme
                    foreach (var etsyReview in EtsyReviewList)
                    {
                        //ilk olarak ilgili yorum hangi ürüne ait skusunu bulup siteden ilgili ürünü bulmak lazım

                        var ilgliTransaction=etsyTransactionsList.Single(x => x.TransactionId == etsyReview.TransactionId);
                        var ilgiliSku = ilgliTransaction.Sku;
                        
                        var ilgiliNopcommerceUrun=await _productService.GetProductBySkuAsync(ilgiliSku);

                        //bu yorum bu ürün için daha önce yapılmışmı kontrol et
                        var productReviewsByproductId = await _productService.GetAllProductReviewsAsync(productId:ilgiliNopcommerceUrun.Id);

                        var buYorumDahaOnceYapildiMi= productReviewsByproductId.ToList().Any(x => x.ReviewText.Contains(etsyReview.Review));

                        if (!buYorumDahaOnceYapildiMi)
                        {
                            
                        }
                    }

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

          

        }




        #endregion
    }
}