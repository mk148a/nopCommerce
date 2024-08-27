
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
using DocumentFormat.OpenXml.Presentation;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Mvc;
using Nop.Core.Domain.Localization;


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






        public async Task<string> RetryTranslateProducts(List<int> productIds)
        {
            string resultText = "";

            var store = await _storeContext.GetCurrentStoreAsync();
            var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var activeLanguages = (await _languageService.GetAllLanguagesAsync(false, storeScope)).Where(x => x.Published).ToList();

            var productsList = await _productService.GetProductsByIdsAsync(productIds.ToArray());

            _translationProgressService.UpdateStartTime(DateTime.UtcNow);
            int i = 0;

            foreach (var product in productsList)
            {
                i++;
                Console.WriteLine("%" + (((double)i / (double)productsList.Count) * 100).ToString("0.00"));
                int percentageComplete = (int)(((double)i / (double)productsList.Count) * 100);
                _translationProgressService.UpdateProgress(percentageComplete, product.Name);

                // HTML içeriğini bir kere dictionary olarak oluştur
                var htmlContentDictionary = await ParseHtmlToList(product.FullDescription);

                var productModel = await _productModelFactory.PrepareProductModelAsync(new ProductModel(), product);

                foreach (var activeLanguage in activeLanguages)
                {
                    var productModelLocale = productModel.Locales.SingleOrDefault(x => x.LanguageId == activeLanguage.Id);
                    if (productModelLocale != null &&
                        (productModelLocale.FullDescription.IsNullOrEmpty() ||
                         productModelLocale.ShortDescription.IsNullOrEmpty() ||
                         productModelLocale.Name.IsNullOrEmpty()))
                    {
                        string twoLetterLangCode = activeLanguage.LanguageCulture.Split('-')[0];

                        TranslateRequest request = new TranslateRequest
                        {
                            Source = "auto",
                            Target = twoLetterLangCode,
                            Text = product.Name
                        };

                        await Task.Delay(500); // Her istekten sonra 500ms bekle
                        var nameTranslationResult = await Translate(request);

                        if (!nameTranslationResult.translation.IsNullOrEmpty())
                        {
                            var translatedName = nameTranslationResult.translation;

                            request.Text = product.ShortDescription;
                            await Task.Delay(500);
                            var shortDescTranslationResult = await Translate(request);

                            if (!shortDescTranslationResult.translation.IsNullOrEmpty())
                            {
                                var translatedShortDescription = shortDescTranslationResult.translation;

                                // HTML içeriğini 4000 karakter limitine göre parçalara ayır ve çevir
                                var translatedFullDescription = await TranslateHtmlContentInChunks(htmlContentDictionary, request);

                                if (!translatedFullDescription.IsNullOrEmpty())
                                {
                                    productModelLocale.Name = translatedName;
                                    productModelLocale.ShortDescription = translatedShortDescription;
                                    productModelLocale.FullDescription = translatedFullDescription;

                                    int index = productModel.Locales.IndexOf(productModelLocale);

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

                    await UpdateLocalesAsync(product, productModel);
                }
            }

            return resultText;
        }

        public async Task<TranslateResponse> Translate(TranslateRequest request)
        {
         
            TranslateResponse result = new TranslateResponse();
            var url = "http://localhost:3000/api/v1/translate";

            var requestBody = new { source = request.Source, target = request.Target, text = request.Text };
            var json = JsonConvert.SerializeObject(requestBody);

            using (var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) })
            {
                var maxRetries = 3;

                for (int retry = 0; retry < maxRetries; retry++)
                {
                    try
                    {
                        var response = await httpClient.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));
                        var responseContent = await response.Content.ReadAsStringAsync();

                        if (response.IsSuccessStatusCode)
                        {
                            // Başarılı bir yanıt, sonucu işle
                            result = JsonConvert.DeserializeObject<TranslateResponse>(responseContent);
                            break; // Başarılı olursa döngüden çık
                        }
                        else
                        {
                            // Yanıt bir hata içeriyor, tüm hata detaylarını logla
                            var errorResponse = JsonConvert.DeserializeObject<TranslateErrorResponse>(responseContent);

                            _translationProgressService.LogError(request.ProductId, request.ProductName, $"Error: {errorResponse.Error}, Details: {errorResponse.Details}");
                        }
                    }
                    catch (HttpRequestException ex)
                    {
                        // Ağ hatası veya başka bir HTTP isteği hatası
                        _translationProgressService.LogError(request.ProductId, request.ProductName, $"Request error: {ex.Message}");
                    }
                    catch (TaskCanceledException ex)
                    {
                        // Timeout durumunda
                        _translationProgressService.LogError(request.ProductId, request.ProductName, $"Timeout error: {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        // Diğer hatalar
                        _translationProgressService.LogError(request.ProductId, request.ProductName, $"Unexpected error: {ex.Message}");
                    }

                    // Hata durumunda belirli bir süre bekleyip yeniden dene
                    await Task.Delay(2000);
                }
            }


            return result;
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

        public async Task<List<TranslationResult>> TranslateProducts()
        {
            var translationResults = new List<TranslationResult>();

            try
            {
                _translationProgressService.UpdateStartTime(DateTime.UtcNow);
                _translationProgressService.UpdateProgress(0, null);

                var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
                var activeLanguages = (await _languageService.GetAllLanguagesAsync(false, storeScope))
                                       .Where(x => x.Published)
                                       .ToList();

                var productsListPage = await _productService.SearchProductsAsync(
                    categoryIds: null,
                    showHidden: false,
                    storeId: storeScope
                );

                var productsList = productsListPage.Where(x => x.Sku == "thumbring1");
                int totalProducts = productsList.Count();
                int i = 0;

                foreach (var product in productsList)
                {
                    i++;
                    int percentageComplete = (int)(((double)i / totalProducts) * 100);
                    _translationProgressService.UpdateProgress(percentageComplete, product.Name);

                    var translationResult = new TranslationResult
                    {
                        ProductId = product.Id,
                        ProductName = product.Name
                    };

                    var needsTranslation = false;

                    // HTML içeriğini bir kere dictionary olarak oluştur
                    var parsedHtmlContent = await ParseHtmlToList(product.FullDescription);

                    foreach (var language in activeLanguages)
                    {
                        var name = await _localizedEntityService.GetLocalizedValueAsync(language.Id, product.Id, "Product", "Name");
                        var shortDescription = await _localizedEntityService.GetLocalizedValueAsync(language.Id, product.Id, "Product", "ShortDescription");
                        var fullDescription = await _localizedEntityService.GetLocalizedValueAsync(language.Id, product.Id, "Product", "FullDescription");

                        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(shortDescription) || string.IsNullOrEmpty(fullDescription))
                        {
                            needsTranslation = true;

                            // Çeviri işlemi
                            var translateRequest = new TranslateRequest
                            {
                                Source = "auto",
                                Target = language.LanguageCulture.Split('-')[0], // Örneğin "en"
                                Text = product.Name
                            };

                            var translationResultName = await Translate(translateRequest);

                            if (!string.IsNullOrEmpty(translationResultName.translation))
                            {
                                await _localizedEntityService.SaveLocalizedValueAsync(product, p => p.Name, translationResultName.translation, language.Id);
                            }
                            else
                            {
                                _translationProgressService.LogError(product.Id, product.Name, $"Name translation failed for {language.Name}");
                                continue; // Bir sonraki dile geç
                            }

                            translateRequest.Text = product.ShortDescription;
                            var translationResultShortDesc = await Translate(translateRequest);

                            if (!string.IsNullOrEmpty(translationResultShortDesc.translation))
                            {
                                await _localizedEntityService.SaveLocalizedValueAsync(product, p => p.ShortDescription, translationResultShortDesc.translation, language.Id);
                            }
                            else
                            {
                                _translationProgressService.LogError(product.Id, product.Name, $"Short Description translation failed for {language.Name}");
                                continue; // Bir sonraki dile geç
                            }

                            // FullDescription için çeviri işlemi
                            var translatedFullDescription = await TranslateHtmlContentInChunks(parsedHtmlContent, translateRequest);

                            if (!string.IsNullOrEmpty(translatedFullDescription))
                            {
                                await _localizedEntityService.SaveLocalizedValueAsync(product, p => p.FullDescription, translatedFullDescription, language.Id);
                            }
                            else
                            {
                                _translationProgressService.LogError(product.Id, product.Name, $"Full Description translation failed for {language.Name}");
                                continue; // Bir sonraki dile geç
                            }
                        }
                        else
                        {
                            // Ürün zaten bu dile çevrilmişse, çeviri dilini kaydet
                            translationResult.TranslatedLanguages.Add(language.Name);
                        }
                    }

                    if (!needsTranslation)
                    {
                        // Ürün zaten tamamen çevrilmişse, bunu listeye ekle
                        translationResults.Add(translationResult);
                    }
                }

                _translationProgressService.StopProgress();
                return translationResults;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Translation process failed: {ex.Message}");
                _translationProgressService.StopProgress();
                throw;
            }
        }

        #region Helper Methods
        public async Task<string> TranslateHtmlContentInChunks(List<HtmlElement> htmlParts, TranslateRequest translateRequest)
        {
            var chunkBuilder = new StringBuilder();
            var translatedContent = new StringBuilder();
            var currentChunkSize = 0;

            foreach (var element in htmlParts)
            {
                // İçeriği metin veya alt elemanlar olarak ayrıştır
                var elementContent = ConvertContentToString(element.Content);

                // Çeviri için parçalara ayırma
                if (element.Translate && !string.IsNullOrWhiteSpace(elementContent))
                {
                    if (currentChunkSize + elementContent.Length > 4000)
                    {
                        // Mevcut chunk'ı translate et
                        translateRequest.Text = chunkBuilder.ToString();
                        var translationResult = await Translate(translateRequest);
                        translatedContent.Append(translationResult.translation);

                        // Yeni bir chunk başlat
                        chunkBuilder.Clear();
                        currentChunkSize = 0;
                    }

                    chunkBuilder.Append(element.OpenTag);
                    chunkBuilder.Append(elementContent);
                    chunkBuilder.Append(element.CloseTag);
                    currentChunkSize += elementContent.Length;
                }
                else
                {
                    // Çevrilmemesi gereken HTML yapılarını direkt ekle
                    translatedContent.Append(element.OpenTag);
                    translatedContent.Append(elementContent);
                    translatedContent.Append(element.CloseTag);
                }
            }

            // Kalan chunk'ı translate et
            if (currentChunkSize > 0)
            {
                translateRequest.Text = chunkBuilder.ToString();
                var translationResult = await Translate(translateRequest);
                translatedContent.Append(translationResult.translation);
            }

            return translatedContent.ToString();
        }

        private string ConvertContentToString(object content)
        {
            if (content is string strContent)
            {
                return strContent;
            }
            else if (content is List<HtmlElement> childElements)
            {
                var stringBuilder = new StringBuilder();
                foreach (var childElement in childElements)
                {
                    stringBuilder.Append(childElement.OpenTag);
                    stringBuilder.Append(ConvertContentToString(childElement.Content));
                    stringBuilder.Append(childElement.CloseTag);
                }
                return stringBuilder.ToString();
            }
            return string.Empty;
        }

        private List<HtmlElement> ParseNode(HtmlNode node)
        {
            var htmlParts = new List<HtmlElement>();

            if (node.NodeType == HtmlNodeType.Element)
            {
                var htmlElement = new HtmlElement
                {
                    OpenTag = $"<{node.Name}{GetAttributesString(node)}>",
                    CloseTag = node.Name.Equals("img", StringComparison.OrdinalIgnoreCase) ||
                               node.Name.Equals("iframe", StringComparison.OrdinalIgnoreCase) ||
                               node.Name.Equals("video", StringComparison.OrdinalIgnoreCase) ||
                               HtmlNode.IsEmptyElement(node.Name) ? "" : $"</{node.Name}>",
                    Translate = true // Varsayılan olarak çevrilecek
                };

                if (node.HasChildNodes)
                {
                    var childParts = new List<HtmlElement>();
                    foreach (var child in node.ChildNodes)
                    {
                        childParts.AddRange(ParseNode(child));
                    }
                    htmlElement.Content = childParts;
                }
                else
                {
                    htmlElement.Content = node.InnerText;
                }

                htmlParts.Add(htmlElement);
            }
            else if (node.NodeType == HtmlNodeType.Text)
            {
                var textContent = node.InnerText.Trim();
                if (!string.IsNullOrEmpty(textContent))
                {
                    htmlParts.Add(new HtmlElement
                    {
                        OpenTag = "",
                        Content = textContent,
                        CloseTag = "",
                        Translate = true
                    });
                }
            }

            return htmlParts;
        }

        private string GetAttributesString(HtmlNode node)
        {
            var attributes = node.Attributes.Select(attr => $"{attr.Name}=\"{attr.Value}\"");
            return attributes.Any() ? " " + string.Join(" ", attributes) : string.Empty;
        }


        public async Task<List<HtmlElement>> ParseHtmlToList(string htmlContent)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(htmlContent);

            // DocumentNode'dan başlayarak tüm HTML öğelerini parse et
            var htmlParts = ParseNode(doc.DocumentNode);

            return htmlParts;
        }





        #endregion

        #endregion
    }
}