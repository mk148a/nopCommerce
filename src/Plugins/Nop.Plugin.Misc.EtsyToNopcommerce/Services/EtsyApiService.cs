using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Spreadsheet;
using Nop.Core;
using Nop.Data;
using Nop.Plugin.Misc.EtsyToNopcommerce.Domains;
using Nop.Plugin.Misc.EtsyToNopcommerce.Models;
using Nop.Plugin.Misc.EtsyToNopcommerce.Models.Receipts;
using Nop.Plugin.Misc.EtsyToNopcommerce.Models.Reviews;
using Nop.Services.Catalog;
using Nop.Services.Configuration;
using RestSharp;


namespace Nop.Plugin.Misc.EtsyToNopcommerce.Services
{
    /// <summary>
    /// Product Reviews Transactions Mapping service
    /// </summary>
    public partial class EtsyApiService : IEtsyApiService
    {
        #region Fields

        private readonly IRepository<EtsyReview> _repository;
        private readonly IStoreContext _storeContext;
        private readonly ISettingService _settingService;
        private readonly IProductReviewsEtsyReviewService _etsyReviewService;
        private readonly IProductService _productService;

        #endregion

        #region Ctor

        public EtsyApiService(IRepository<EtsyReview> repository, IStoreContext storeContext, ISettingService settingService, IProductReviewsEtsyReviewService etsyReviewService, IProductService productService)
        {
            _repository = repository;
            _storeContext = storeContext;
            _settingService = settingService;
            _etsyReviewService=etsyReviewService;
            _productService = productService;
        }

        #endregion




        #region CRUD methods

      

     


        public virtual async Task<string> GetAllEtsyReviews()
        {
            string result = "";
            int addedEtsyReviews = 0;
            var storeId = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var currentStore = await _storeContext.GetCurrentStoreAsync();
            storeId = currentStore.Id;
            var ayar = await _settingService.LoadSettingAsync<EtsyToNopcommerceSettings>(storeId);
            string BaseAdres = "https://openapi.etsy.com/";

            string requestGetShopReviewsUrl = $"v3/application/shops/{ayar.ShopId}/reviews";
            await CheckTokenExpire(true);
            await _settingService.ClearCacheAsync();

            ayar = await _settingService.LoadSettingAsync<EtsyToNopcommerceSettings>(storeId);


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

                    var reviewsList = await WhenAllEx(getReviewssJobs);
                   

                    //Todo:Buraya receipts çağırılıp sku kontröllü review filtrelemesi yapacağız
                    var receipts = await GetAllEtsyReceipts();

                    foreach (var reviewResults in reviewsList)
                    {
                        foreach (var review in reviewResults)
                        {
                            if (review != null && review.TransactionId != null)
                            {
                                var reviewVarmi = await _etsyReviewService.AskEtsyReviewByTransactionIdAsync(review.TransactionId.Value);
                                if (!reviewVarmi)
                                {
                                    if (receipts.Any(x=>x.Transactions.Any(y=>y.TransactionId== review.TransactionId.Value)) )
                                    {
                                        var ilgiliReceipt = receipts.Single(x => x.Transactions.Any(y => y.TransactionId == review.TransactionId));
                                        string ilgiliSku =
                                            (from m in ilgiliReceipt.Transactions
                                                where m.TransactionId == review.TransactionId
                                                select (string)m.Sku)
                                            .FirstOrDefault();
                                        var newAddedEtsyReview = await _etsyReviewService.InsertEtsyReviewAsync(review.ShopId, review.TransactionId.Value,
                                            review.ListingId, review.BuyerUserId,
                                            review.Rating, review.Review, review.Language, review.ImageUrlFullxfull,
                                            review.CreateTimestamp, review.CreatedTimestamp,
                                            review.UpdateTimestamp, review.UpdatedTimestamp, ilgiliSku);

                                        addedEtsyReviews += 1;

                                    }
                                    
                                }

                            }
                        }
                    }

                }
            }
            else
            {
                await CheckTokenExpire(false);
            }

            result = addedEtsyReviews + " adet Etsy Yorumu Veritabanına Eklendi";
            return result;
        }

        public virtual async Task<HashSet<Receipt>> GetAllEtsyReceipts()
        {
            HashSet<Receipt> result =new HashSet<Receipt>();
          

            var storeId = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var currentStore = await _storeContext.GetCurrentStoreAsync();
            storeId = currentStore.Id;
            var ayar = await _settingService.LoadSettingAsync<EtsyToNopcommerceSettings>(storeId);
            string BaseAdres = "https://openapi.etsy.com/";

            string requestGetShopReceiptsUrl = $"v3/application/shops/{ayar.ShopId}/receipts";
            await CheckTokenExpire(true);
            await _settingService.ClearCacheAsync();

            ayar = await _settingService.LoadSettingAsync<EtsyToNopcommerceSettings>(storeId);


            List<KeyValuePair<string, string>> getShopReceiptsParameters = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("limit", "1")
            };
            var getShopReceiptsResponse = await EtsyRequests(requestGetShopReceiptsUrl, getShopReceiptsParameters);

            if (getShopReceiptsResponse.StatusCode == System.Net.HttpStatusCode.OK)
            {
                var getShopReceiptsResult =
                    Newtonsoft.Json.JsonConvert.DeserializeObject<Receipts>(
                        getShopReceiptsResponse.Content);

                if (getShopReceiptsResult.Count > 0)
                {
                    int maxcount = int.Parse(getShopReceiptsResult.Count.ToString());
                    double sirasayisi = 0;
                    double x = (double)maxcount / (double)100;
                    sirasayisi = Math.Round(x, 0);
                    if (sirasayisi < 1)
                    {
                        sirasayisi = 1;
                    }

                    List<Task<HashSet<Receipt>?>> getReceiptsJobs = new List<Task<HashSet<Receipt>>>();

                    for (int i = 0; i < sirasayisi + 2; i++)
                    {
                        getShopReceiptsParameters.Clear();
                        getShopReceiptsParameters.Add(new KeyValuePair<string, string>("limit", "100"));
                        getShopReceiptsParameters.Add(new KeyValuePair<string, string>("offset", (i * 100).ToString()));
                        getReceiptsJobs.Add(GetEtsyReceipts(requestGetShopReceiptsUrl, getShopReceiptsParameters));
                    }

                    var receiptList = await WhenAllEx(getReceiptsJobs);
                  

                    foreach (var receipts in receiptList)
                    {
                        foreach (var receipt in receipts)
                        {
                            if (receipt!=null)
                            {
                                if (receipt.Transactions!=null)
                                {
                                    if (receipt.Transactions.Count>0)
                                    {
                                        //Todo:burada nopcommerce ile etsy receipts arasında sku kontrölü yapılacak
                                        foreach (var transaction in receipt.Transactions)
                                        {
                                            var ilgiliNopcommerceUrun = await _productService.GetProductBySkuAsync(transaction.Sku);

                                            if (ilgiliNopcommerceUrun != null)
                                            {
                                                result.Add(receipt);
                                            }

                                           
                                        }
                                       
                                    }
                                }
                            }
                        }
                    }

                }
            }
            else
            {
                await CheckTokenExpire(false);
            }

            return result;
        }

        #endregion

            #region Helper Methods
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
        public async Task<IRestResponse> EtsyRequests(string url, List<KeyValuePair<string, string>> parameters)
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
        public async Task CheckTokenExpire(bool authourised)
        {
            var storeId = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var ayar = await _settingService.LoadSettingAsync<EtsyToNopcommerceSettings>(storeId);

            if (ayar != null)
            {

                string requestGetTokenUrl = "v3/public/oauth/token";

                var suan = DateTime.Now;
                DateTime? tokenDate= ayar.TokenDate; ;
                if (ayar.TokenDate!=null)
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

        public async Task<HashSet<EtsyReview>?> GetEtsyReviews(string requestReviewsUrl, List<KeyValuePair<string, string>> getReviewsParameters)
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
                                getReviewsResponse.Content.Replace("&amp;", "&").Replace("Etsy", "web site"));
                    var newResult = new HashSet<EtsyReview>();


                    foreach (var result in getReviewsResult.Results)
                    {
                        result.Review = System.Web.HttpUtility.HtmlDecode(result.Review);
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

        public async Task<HashSet<Receipt>?> GetEtsyReceipts(string requestGetShopReceiptsUrl, List<KeyValuePair<string, string>> getShopReceiptsParameters)
        {
            IRestResponse getReceiptsResponse;

            Receipts getReceiptsResult;
            getReceiptsResponse =
                await EtsyRequests(requestGetShopReceiptsUrl, getShopReceiptsParameters);

            if (getReceiptsResponse.StatusCode == HttpStatusCode.OK)
            {
                try
                {
                    getReceiptsResult =
                        Newtonsoft.Json.JsonConvert
                            .DeserializeObject<Receipts>(
                                getReceiptsResponse.Content);


                    return getReceiptsResult.Results;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }

            return null;
        }

        #endregion
    }
}
