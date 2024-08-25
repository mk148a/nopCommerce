
using Newtonsoft.Json;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Plugin.Misc.NopTranslator.Models;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Seo;
using Nop.Web.Areas.Admin.Models.Catalog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Nop.Web.Areas.Admin.Factories;
using LinqToDB.Common;
using System.Text.RegularExpressions;

namespace Nop.Plugin.Misc.NopTranslator.Services
{
    public class TranslateService : ITranslateService
    {
        #region Fields
        private IProductService _productService;
        private ILocalizedEntityService _localizedEntityService;
        private IUrlRecordService _urlRecordService;
        private IProductModelFactory _productModelFactory;
        private ILanguageService _languageService;
        private readonly IStoreContext _storeContext;
        private readonly ITranslationProgressService _translationProgressService;

        #endregion

        #region Ctor

        public TranslateService(IProductService productService, ILocalizedEntityService localizedEntityService,
            IUrlRecordService urlRecordService,
            IProductModelFactory productModelFactory, ILanguageService languageService, IStoreContext storeContext, ITranslationProgressService translationProgressService)
        {
            _productService = productService;
            _localizedEntityService = localizedEntityService;
            _urlRecordService = urlRecordService;
            _productModelFactory = productModelFactory;
            _languageService = languageService;
            _storeContext = storeContext;
            _translationProgressService = translationProgressService;
        }

        #endregion

        #region Methods
        #region MyRegion

        public List<string> SplitHtmlText(string htmlText, int maxChunkSize = 4000)
        {
            List<string> chunks = new List<string>();
            int currentIndex = 0;

            while (currentIndex < htmlText.Length)
            {
                int nextChunkSize = Math.Min(maxChunkSize, htmlText.Length - currentIndex);
                string nextChunk = GetNextChunk(htmlText, currentIndex, nextChunkSize);
                chunks.Add(nextChunk);
                currentIndex += nextChunk.Length;
            }

            return chunks;
        }

        private string GetNextChunk(string htmlText, int startIndex, int chunkSize)
        {
            // Chunking işlemini daha küçük parçalara bölerek yapıyoruz
            int endIndex = startIndex + chunkSize;

            if (endIndex >= htmlText.Length)
                return htmlText.Substring(startIndex);

            // Sonraki bölümü güvenli bir yerden kesmek için en yakın noktalama işaretini arıyoruz
            int lastSafeBreak = FindLastSafeBreak(htmlText, startIndex, endIndex);

            if (lastSafeBreak == -1)
                return htmlText.Substring(startIndex, chunkSize);

            return htmlText.Substring(startIndex, lastSafeBreak - startIndex + 1);
        }

        private int FindLastSafeBreak(string htmlText, int startIndex, int endIndex)
        {
            // Noktalama işaretlerini, HTML taglerini ve boşlukları güvenli kırılma noktaları olarak kullanıyoruz
            string pattern = @"[.!?](?!</)|(</p>)|(</div>)";
            MatchCollection matches = Regex.Matches(htmlText.Substring(startIndex, endIndex - startIndex), pattern, RegexOptions.RightToLeft);

            if (matches.Count > 0)
            {
                return startIndex + matches[0].Index + matches[0].Length - 1;
            }

            return -1;
        }
        public async Task<TranslateResponse> Translate(TranslateRequest request)
        {
            TranslateResponse result = new TranslateResponse();
            var url = "http://localhost:3000/api/v1/translate";

            var requestBody = new { source = request.Source, target = request.Target, text = request.Text };
            var json = JsonConvert.SerializeObject(requestBody);

            var httpClient = new HttpClient();
            var maxRetries = 3;

            for (int retry = 0; retry < maxRetries; retry++)
            {
                var response = await httpClient.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    result = JsonConvert.DeserializeObject<TranslateResponse>(responseContent);
                    break; // Başarılı olursa döngüden çık
                }
                else
                {
                    var errorMessage = await response.Content.ReadAsStringAsync();
                    Console.WriteLine(errorMessage);
                    await Task.Delay(2000); // 2 saniye bekle ve yeniden dene
                }
            }

            return result;
        }
        public async Task<string> TranslateLargeText(TranslateRequest request)
        {
            var chunks = SplitHtmlText(request.Text, 4000);
            StringBuilder translatedText = new StringBuilder();

            foreach (var chunk in chunks)
            {
                request.Text = chunk;
                var result = await Translate(request);
                if (!string.IsNullOrEmpty(result.translation))
                {
                    translatedText.Append(result.translation);
                }
                else
                {
                    throw new Exception("Translation failed for chunk.");
                }
            }

            return translatedText.ToString();
        }
        protected virtual async Task UpdateLocalesAsync(Product product, ProductModel model)
        {
            foreach (var localized in model.Locales)
            {
                await _localizedEntityService.SaveLocalizedValueAsync(product,
                    x => x.Name,
                    localized.Name,
                    localized.LanguageId);
                await _localizedEntityService.SaveLocalizedValueAsync(product,
                    x => x.ShortDescription,
                    localized.ShortDescription,
                    localized.LanguageId);
                await _localizedEntityService.SaveLocalizedValueAsync(product,
                    x => x.FullDescription,
                    localized.FullDescription,
                    localized.LanguageId);
                await _localizedEntityService.SaveLocalizedValueAsync(product,
                    x => x.MetaKeywords,
                    localized.MetaKeywords,
                    localized.LanguageId);
                await _localizedEntityService.SaveLocalizedValueAsync(product,
                    x => x.MetaDescription,
                    localized.MetaDescription,
                    localized.LanguageId);
                await _localizedEntityService.SaveLocalizedValueAsync(product,
                    x => x.MetaTitle,
                    localized.MetaTitle,
                    localized.LanguageId);

                //search engine name
                var seName = await _urlRecordService.ValidateSeNameAsync(product, localized.SeName, localized.Name, false);
                await _urlRecordService.SaveSlugAsync(product, seName, localized.LanguageId);
            }
        }

        #endregion
        public async Task<string> TranslateProducts()
        {
            string resultText = "";

            var store = await _storeContext.GetCurrentStoreAsync();
            var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var activeLanguages =
                (await _languageService.GetAllLanguagesAsync(false, storeScope)).Where(x => x.Published = true);

            //Get all active products by current store
            var productsList = await _productService.SearchProductsAsync(
                categoryIds: null,
                showHidden: false,
                storeId: storeScope
            );


            _translationProgressService.UpdateStartTime(DateTime.UtcNow);
            int i = 0;
            foreach (var product in productsList)
            {
                i++;
                Console.WriteLine("%" + (((double)i / (double)productsList.Count) * 100).ToString("0.00"));
                // İşlem durumunu güncelle
                int percentageComplete = (int)(((double)i / (double)productsList.Count) * 100);
                _translationProgressService.UpdateProgress(percentageComplete, product.Name);
                var productModel = new ProductModel();

                //Prepare Product Model for add localized product fields and slugs
                productModel = await _productModelFactory.PrepareProductModelAsync(productModel, product);

                foreach (var activeLanguage in activeLanguages)
                {
                    bool isLanguageActive = productModel.Locales.Any(x => x.LanguageId == activeLanguage.Id);
                    if (isLanguageActive)
                    {
                        var productModelLocale = productModel.Locales.Single(x => x.LanguageId == activeLanguage.Id);
                        if (productModelLocale.FullDescription.IsNullOrEmpty() ||
                            productModelLocale.ShortDescription.IsNullOrEmpty() || productModelLocale.Name.IsNullOrEmpty())
                        {
                            //get language cultures first two letters
                            string twoLetterLangCode = activeLanguage.LanguageCulture.Split("-")[0];


                            TranslateRequest request = new TranslateRequest();
                            request.Source = "auto";
                            request.Target = twoLetterLangCode;
                            request.Text = product.Name;
                            // Hız sınırlama için gecikme
                            await Task.Delay(500); // Her istekten sonra 500ms bekle
                            var result = await Translate(request);


                            if (!result.translation.IsNullOrEmpty())
                            {
                                var translatedName = result.translation;

                                request.Text = product.ShortDescription;
                                // Hız sınırlama için gecikme
                                await Task.Delay(500); // Her istekten sonra 500ms bekle
                                result = await Translate(request);

                                if (!result.translation.IsNullOrEmpty())
                                {
                                    var translatedShortDescription = result.translation;

                                    request.Text = product.FullDescription;
                                    // Hız sınırlama için gecikme
                                    await Task.Delay(500); // Her istekten sonra 500ms bekle
                                    var translatedFullDescription  = await TranslateLargeText(request);

                                    if (!translatedFullDescription.IsNullOrEmpty())
                                    {
                                       

                                        //Add the Product Model Locale

                                        productModelLocale.Name = translatedName;
                                        productModelLocale.ShortDescription = translatedShortDescription;
                                        productModelLocale.FullDescription = translatedFullDescription;

                                        int index = productModel.Locales.IndexOf(
                                            productModel.Locales.Single(x => x.LanguageId == activeLanguage.Id));

                                        if (index != -1)
                                            productModel.Locales[index] = productModelLocale;
                                    }
                                    else
                                    {
                                        resultText += Environment.NewLine + product.Name +
                                                      " named product not translated because FullDescription not translated";
                                    }
                                }
                                else
                                {
                                    resultText += Environment.NewLine + product.Name +
                                                  " named product not translated because ShortDescription not translated";
                                }



                            }
                            else
                            {
                                resultText += Environment.NewLine + product.Name +
                                              " named product not translated because ProductName not translated";
                            }


                        }
                        else
                        {
                            resultText += Environment.NewLine + product.Name +
                                          " named product not translated because ProductName or Descriptions not empty";
                        }
                    }

                    await UpdateLocalesAsync(product, productModel);


                }
            }

            return resultText;
        }


        #endregion
    }
}