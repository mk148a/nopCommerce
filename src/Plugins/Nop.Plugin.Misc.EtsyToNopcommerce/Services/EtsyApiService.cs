using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using LinqToDB.Common;
using Newtonsoft.Json;
using Nop.Core;
using Nop.Data;
using Nop.Plugin.Misc.EtsyToNopcommerce.Domains;
using Nop.Plugin.Misc.EtsyToNopcommerce.Models;
using Nop.Services.Catalog;
using Nop.Services.Configuration;
using RestSharp;

using Nop.Plugin.Misc.EtsyToNopcommerce.Models.Etsy.Listings;
using Nop.Plugin.Misc.EtsyToNopcommerce.Models.Etsy;


namespace Nop.Plugin.Misc.EtsyToNopcommerce.Services
{
    /// <summary>
    /// Product Reviews Transactions Mapping service
    /// </summary>
    public partial class EtsyApiService : IEtsyApiService
    {
        #region Fields

        private readonly IStoreContext _storeContext;
        private readonly ISettingService _settingService;
        private readonly IProductReviewsEtsyReviewService _etsyReviewService;
        private readonly IProductService _productService;
        private readonly IEtsyListingsService _etsyListingsService;
        private readonly IEtsyCustomersService _etsyCustomersService;

        #endregion

        #region Ctor

        public EtsyApiService( IStoreContext storeContext, ISettingService settingService, 
            IProductReviewsEtsyReviewService etsyReviewService, IProductService productService, IEtsyListingsService etsyListingsService, IEtsyCustomersService etsyCustomersService)
        {
            _storeContext = storeContext;
            _settingService = settingService;
            _etsyReviewService=etsyReviewService;
            _productService = productService;
            _etsyListingsService=etsyListingsService;
            _etsyCustomersService=etsyCustomersService;
        }

        #endregion




        #region CRUD methods

      

     


        public virtual async Task<string> GetAllEtsyReviews(bool onlyAddNopcommerceProduct = true)
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
            HashSet<EtsyReview> etsyReviews = new HashSet<EtsyReview>();
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
                    var receipts = await GetAllEtsyReceipts(onlyAddNopcommerceProduct);

                    
                    if (onlyAddNopcommerceProduct)
                    {
                        foreach (var reviewResults in reviewsList)
                    {
                        if (reviewResults != null)
                        { 
                            foreach (var review in reviewResults)
                            {
                                if (review != null && review.TransactionId != null)
                                {
                                   
                                        var reviewVarmi =
                                            await _etsyReviewService.AskEtsyReviewByTransactionIdAsync(review.TransactionId
                                                .Value);

                                        if (!reviewVarmi)
                                        {
                                            if (receipts.Any(x =>
                                                    x.Transactions.Any(y => y.TransactionId == review.TransactionId.Value)))
                                            {
                                                var ilgiliReceipt = receipts.Single(x =>
                                                    x.Transactions.Any(y => y.TransactionId == review.TransactionId));
                                                string ilgiliSku =
                                                    (from m in ilgiliReceipt.Transactions
                                                     where m.TransactionId == review.TransactionId
                                                     select (string)m.Sku)
                                                    .FirstOrDefault();
                                                var newAddedEtsyReview = await _etsyReviewService.InsertEtsyReviewAsync(
                                                    review.ShopId, review.TransactionId.Value,
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
                    //This feature is for GetAllEtsyCustomers Function
                    else
                    {
                        foreach (var reviewResults in reviewsList)
                        {
                            if (reviewResults != null)
                            {
                                foreach (var review in reviewResults)
                                {
                                    if (review != null && review.TransactionId != null)
                                    {
                                        etsyReviews.Add(review);
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
            if (!onlyAddNopcommerceProduct)
            {
                try
                {
                    result = JsonConvert.SerializeObject(etsyReviews);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
            return result;
        }

        public virtual async Task<HashSet<Receipt>> GetAllEtsyReceipts(bool onlyAddNopcommerceProduct=true)
        {
            HashSet<Receipt> result = new HashSet<Receipt>();


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
                        if (receipts!=null)
                        {
                            foreach (var receipt in receipts)
                            {
                                if (receipt != null)
                                {
                                    if (receipt.Transactions != null)
                                    {
                                        if (receipt.Transactions.Count > 0)
                                        {
                                            //Todo:burada nopcommerce ile etsy receipts arasında sku kontrölü yapılacak
                                            foreach (var transaction in receipt.Transactions)
                                            {
                                                var ilgiliNopcommerceUrun = await _productService.GetProductBySkuAsync(transaction.Sku);

                                                if (onlyAddNopcommerceProduct)
                                                {

                                                    if (ilgiliNopcommerceUrun != null)
                                                    {
                                                        result.Add(receipt);
                                                    }
                                                }
                                                //We add this because we will use this function for "GetEtsyCustomers" service
                                                else
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
            }
            else
            {
                await CheckTokenExpire(false);
            }

            return result;
        }

        public virtual async Task<string> GetAllEtsyCustomers()
        {
            int etsyNewCustomerCount=0, etsyCustomerUpdateCount=0;
            string result = "";

            var allEtsyOrders =await GetAllEtsyReceipts(false);
            var allEtsyReviewsString=await GetAllEtsyReviews(false);
            HashSet<EtsyReview> allEtsyReviews = new HashSet<EtsyReview>();

            try
            {
                allEtsyReviews = JsonConvert.DeserializeObject<HashSet<EtsyReview>>(allEtsyReviewsString);

            }
            catch (Exception e)
            {
                Console.WriteLine(e);

            }

            foreach (var etsyOrder in allEtsyOrders)
            {
                EtsyCustomer etsyCustomer = new EtsyCustomer();

                var buyerId =etsyOrder.BuyerUserId;
                bool isAdded = await _etsyCustomersService.AskEtsyCustomerByBuyerUserIdAsync(buyerId.Value);
                if (!isAdded)
                {
                    etsyCustomer.BuyerUserId=buyerId.Value;
                    etsyCustomer.BuyerEmail = etsyOrder.BuyerEmail;
                    etsyCustomer.BuyerName = etsyOrder.Name;
                    etsyCustomer.RatingAndReviews = "";
                    etsyCustomer.City=etsyOrder.City;
                    etsyCustomer.State=etsyOrder.State;
                    etsyCustomer.Country = etsyOrder.CountryIso;
                    etsyCustomer.ZipCode = etsyOrder.Zip;
                    foreach (var transaction in etsyOrder.Transactions)
                    {
                        etsyCustomer.OrderedItems += transaction.Title + " (" + transaction.Sku + ")" + " (" + transaction.TransactionId + ") ; ";
                        try
                        {

                     
                        if (allEtsyReviews.Any(x => x.TransactionId == transaction.TransactionId))
                        {
                            var review= allEtsyReviews.Where(x=>x.TransactionId==transaction.TransactionId).FirstOrDefault();
                            if (review != null)
                            {
                                etsyCustomer.RatingAndReviews += review.Rating + " / " + review.Review + " ("+ transaction.TransactionId+") ; ";
                            }
                        }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                        }
                    }


                    await _etsyCustomersService.InsertEtsyCustomerAsync(etsyCustomer);
                    etsyNewCustomerCount += 1;
                }
                else
                {
                    etsyCustomer =  await _etsyCustomersService.GetCustomerByBuyerUserIdAsync(buyerId.Value);
                    foreach (var transaction in etsyOrder.Transactions)
                    {
                        if (string.IsNullOrEmpty(etsyCustomer.OrderedItems))
                        {
                            etsyCustomer.OrderedItems = "";
                        }
                        if (!etsyCustomer.OrderedItems.Contains(transaction.TransactionId.ToString()))
                        {
                            etsyCustomer.OrderedItems += transaction.Title + " (" + transaction.Sku + ") ; " + " (" + transaction.TransactionId + ") ; ";

                        }
                        try
                        {
                            if (string.IsNullOrEmpty(etsyCustomer.RatingAndReviews))
                            {
                                etsyCustomer.RatingAndReviews = "";
                            }

                            if (!etsyCustomer.RatingAndReviews.Contains(transaction.TransactionId.ToString()))
                            {
                                if (allEtsyReviews.Any(x => x.TransactionId == transaction.TransactionId))
                                {
                                    var review = allEtsyReviews.Where(x => x.TransactionId == transaction.TransactionId).FirstOrDefault();
                                    if (review != null)
                                    {
                                        etsyCustomer.RatingAndReviews += review.Rating + " / " + review.Review + " (" + transaction.TransactionId + ") ; ";
                                    }
                                }
                            }

                           
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                        }
                    }
                    await _etsyCustomersService.UpdateEtsyCustomerAsync(etsyCustomer);
                    etsyCustomerUpdateCount += 1;
                }

               


            }

            result = "Toplam " + etsyNewCustomerCount + " adet yeni Müşteri Eklendi." + " Ayrıca toplam " +
                     etsyCustomerUpdateCount + " adet müşteri güncellendi.";

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

        public async Task<HashSet<EtsyListing>?> GetEtsyListings(string requestListingsUrl, List<KeyValuePair<string, string>> getListingsParameters)
        {
            IRestResponse getListingsResponse;

            GetListingsById getListingsResult;
            getListingsResponse =
                await EtsyRequests(requestListingsUrl, getListingsParameters);

            if (getListingsResponse.StatusCode == HttpStatusCode.OK)
            {
                try
                {
                    getListingsResult =
                        Newtonsoft.Json.JsonConvert
                            .DeserializeObject<GetListingsById>(
                                getListingsResponse.Content.Replace("&amp;", "&"));
                    var newResult = new HashSet<EtsyListing>();


                    foreach (var result in getListingsResult.Results)
                    {
                        result.Description = System.Web.HttpUtility.HtmlDecode(result.Description);
                        result.Title = System.Web.HttpUtility.HtmlDecode(result.Title);
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

      

        public async Task<string> ListingleriAlVeIsle()
        {
            var storeId = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var currentStore = await _storeContext.GetCurrentStoreAsync();
            storeId = currentStore.Id;
            var ayar = await _settingService.LoadSettingAsync<EtsyToNopcommerceSettings>(storeId);
            string BaseAdres = "https://openapi.etsy.com/";

            //https://openapi.etsy.com/v3/application/shops/21815852/listings?state=inactive&
            string requestGetShopListingsUrl = $"v3/application/shops/{ayar.ShopId}/listings";


            int reviewsAdded = 0;
            int reviewsWithPhotoAdded = 0;


            await CheckTokenExpire(true);
            await _settingService.ClearCacheAsync();
            ayar = await _settingService.LoadSettingAsync<EtsyToNopcommerceSettings>(storeId);

            #region Etsy İLanları Çagırma Bölümü

            //inactive listings
            List<KeyValuePair<string, string>> getListingsParameters = new List<KeyValuePair<string, string>>
                        {
                            new KeyValuePair<string, string>("limit", "1"),
                            new KeyValuePair<string, string>("state", "inactive")
                        };
            var getShopListingsResponse = await EtsyRequests(requestGetShopListingsUrl, getListingsParameters);

            if (getShopListingsResponse.StatusCode == System.Net.HttpStatusCode.OK)
            {
                var getShopListingsResult =
                    Newtonsoft.Json.JsonConvert.DeserializeObject<GetListingsById>(
                        getShopListingsResponse.Content.Replace("&amp;", "&"));

                if (getShopListingsResult.Count > 0)
                {
                    int maxcount = int.Parse(getShopListingsResult.Count.ToString());
                    double sirasayisi = 0;
                    double x = (double)maxcount / (double)100;
                    sirasayisi = Math.Round(x, 0);
                    if (sirasayisi < 1)
                    {
                        sirasayisi = 1;
                    }

                    List<Task<HashSet<EtsyListing>?>> getListingsJobs = new List<Task<HashSet<EtsyListing>>>();

                    for (int i = 0; i < sirasayisi + 2; i++)
                    {
                        getListingsParameters.Clear();
                        getListingsParameters.Add(new KeyValuePair<string, string>("state", "inactive"));
                        getListingsParameters.Add(new KeyValuePair<string, string>("limit", "100"));
                        getListingsParameters.Add(new KeyValuePair<string, string>("offset", (i * 100).ToString()));
                        getListingsJobs.Add(GetEtsyListings(requestGetShopListingsUrl, getListingsParameters));
                    }

                    //active listings
                    getListingsParameters = new List<KeyValuePair<string, string>>
                    {
                        new KeyValuePair<string, string>("limit", "1"),
                        new KeyValuePair<string, string>("state", "active")
                    };
                    getShopListingsResponse = await EtsyRequests(requestGetShopListingsUrl, getListingsParameters);

                    if (getShopListingsResponse.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        getShopListingsResult =
                           Newtonsoft.Json.JsonConvert.DeserializeObject<GetListingsById>(
                               getShopListingsResponse.Content.Replace("&amp;", "&"));

                        if (getShopListingsResult.Count > 0)
                        {
                            maxcount = int.Parse(getShopListingsResult.Count.ToString());
                            sirasayisi = 0;
                            x = (double)maxcount / (double)100;
                            sirasayisi = Math.Round(x, 0);
                            if (sirasayisi < 1)
                            {
                                sirasayisi = 1;
                            }

                            for (int i = 0; i < sirasayisi + 2; i++)
                            {
                                getListingsParameters.Clear();
                                getListingsParameters.Add(new KeyValuePair<string, string>("state", "active"));
                                getListingsParameters.Add(new KeyValuePair<string, string>("limit", "100"));
                                getListingsParameters.Add(new KeyValuePair<string, string>("offset", (i * 100).ToString()));
                                getListingsJobs.Add(GetEtsyListings(requestGetShopListingsUrl, getListingsParameters));
                            }
                        }


                    }


                    //sold_out listings
                    getListingsParameters = new List<KeyValuePair<string, string>>
                    {
                        new KeyValuePair<string, string>("limit", "1"),
                        new KeyValuePair<string, string>("state", "sold_out")
                    };
                    getShopListingsResponse = await EtsyRequests(requestGetShopListingsUrl, getListingsParameters);

                    if (getShopListingsResponse.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        getShopListingsResult =
                            Newtonsoft.Json.JsonConvert.DeserializeObject<GetListingsById>(
                                getShopListingsResponse.Content.Replace("&amp;", "&"));

                        if (getShopListingsResult.Count > 0)
                        {
                            maxcount = int.Parse(getShopListingsResult.Count.ToString());
                            sirasayisi = 0;
                            x = (double)maxcount / (double)100;
                            sirasayisi = Math.Round(x, 0);
                            if (sirasayisi < 1)
                            {
                                sirasayisi = 1;
                            }

                            for (int i = 0; i < sirasayisi + 2; i++)
                            {
                                getListingsParameters.Clear();
                                getListingsParameters.Add(new KeyValuePair<string, string>("state", "sold_out"));
                                getListingsParameters.Add(new KeyValuePair<string, string>("limit", "100"));
                                getListingsParameters.Add(new KeyValuePair<string, string>("offset", (i * 100).ToString()));
                                getListingsJobs.Add(GetEtsyListings(requestGetShopListingsUrl, getListingsParameters));
                            }
                        }


                    }


                    //removed listings
                    getListingsParameters = new List<KeyValuePair<string, string>>
                    {
                        new KeyValuePair<string, string>("limit", "1"),
                        new KeyValuePair<string, string>("state", "removed")
                    };
                    getShopListingsResponse = await EtsyRequests(requestGetShopListingsUrl, getListingsParameters);

                    if (getShopListingsResponse.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        getShopListingsResult =
                            Newtonsoft.Json.JsonConvert.DeserializeObject<GetListingsById>(
                                getShopListingsResponse.Content.Replace("&amp;", "&"));

                        if (getShopListingsResult.Count > 0)
                        {
                            maxcount = int.Parse(getShopListingsResult.Count.ToString());
                            sirasayisi = 0;
                            x = (double)maxcount / (double)100;
                            sirasayisi = Math.Round(x, 0);
                            if (sirasayisi < 1)
                            {
                                sirasayisi = 1;
                            }

                            for (int i = 0; i < sirasayisi + 2; i++)
                            {
                                getListingsParameters.Clear();
                                getListingsParameters.Add(new KeyValuePair<string, string>("state", "removed"));
                                getListingsParameters.Add(new KeyValuePair<string, string>("limit", "100"));
                                getListingsParameters.Add(new KeyValuePair<string, string>("offset", (i * 100).ToString()));
                                getListingsJobs.Add(GetEtsyListings(requestGetShopListingsUrl, getListingsParameters));
                            }
                        }


                    }


                    //expired listings
                    getListingsParameters = new List<KeyValuePair<string, string>>
                    {
                        new KeyValuePair<string, string>("limit", "1"),
                        new KeyValuePair<string, string>("state", "expired")
                    };
                    getShopListingsResponse = await EtsyRequests(requestGetShopListingsUrl, getListingsParameters);

                    if (getShopListingsResponse.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        getShopListingsResult =
                            Newtonsoft.Json.JsonConvert.DeserializeObject<GetListingsById>(
                                getShopListingsResponse.Content.Replace("&amp;", "&"));

                        if (getShopListingsResult.Count > 0)
                        {
                            maxcount = int.Parse(getShopListingsResult.Count.ToString());
                            sirasayisi = 0;
                            x = (double)maxcount / (double)100;
                            sirasayisi = Math.Round(x, 0);
                            if (sirasayisi < 1)
                            {
                                sirasayisi = 1;
                            }

                            for (int i = 0; i < sirasayisi + 2; i++)
                            {
                                getListingsParameters.Clear();
                                getListingsParameters.Add(new KeyValuePair<string, string>("state", "expired"));
                                getListingsParameters.Add(new KeyValuePair<string, string>("limit", "100"));
                                getListingsParameters.Add(new KeyValuePair<string, string>("offset", (i * 100).ToString()));
                                getListingsJobs.Add(GetEtsyListings(requestGetShopListingsUrl, getListingsParameters));
                            }
                        }


                    }






                    var listingsList = await WhenAllEx(getListingsJobs);
                    var EtsyListingsList = new HashSet<EtsyListing>();

                    foreach (var listingResults in listingsList)
                    {
                        foreach (var listing in listingResults)
                        {
                            if (listing != null)
                            {
                                EtsyListingsList.Add(listing);

                                var reviewVarmi = await _etsyListingsService.AskEtsyListingByIdAsync(listing.ListingId);
                                if (!reviewVarmi)
                                {
                                    try
                                    {

                                    var result = await _etsyListingsService.InsertEtsyListingAsync(listing);
                                    reviewsAdded += 1;

                                    }
                                    catch (Exception e)
                                    {
                                        Console.WriteLine(e);
                                    }
                                }

                            }

                        }
                    }




                    Console.WriteLine("Toplam " + reviewsAdded + " adet ilan eklendi");
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
