
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
        private const string SpecialSeparator = "##001##"; // Özel ayırıcı karakter

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

                var translationGroups = CreateTranslationGroups(htmlContentDictionary); // Gruplama işlemi
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
                                var translatedFullDescription = await TranslateHtmlContentInChunks(translationGroups, request);

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

            using (var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(20) })
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

                    // Çeviriye hazır dictionary grupları oluştur
                    var translationGroups = CreateTranslationGroups(parsedHtmlContent);


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
                            var translatedFullDescription = await TranslateHtmlContentInChunks(translationGroups, translateRequest);

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
        public async Task<string> TranslateHtmlContentInChunks(List<List<Dictionary<string, object>>> translationGroups, TranslateRequest translateRequest)
        {
            var translatedContent = new StringBuilder();

            foreach (var group in translationGroups)
            {
                var textToTranslate = string.Join(SpecialSeparator, group.Where(d => (bool)d["translate"]).Select(d => ConvertContentToString(d["content"])));


                // Eğer çevrilecek metin yoksa, bu grubu atla
                if (string.IsNullOrWhiteSpace(textToTranslate))
                {
                    foreach (var dict in group)
                    {
                        translatedContent.Append(dict["openTag"]);
                        translatedContent.Append(ConvertContentToString(dict["content"]));
                        translatedContent.Append(dict["closeTag"]);
                    }
                    continue;
                }

                translateRequest.Text = textToTranslate;
                var translationResult = await Translate(translateRequest);

                if (!string.IsNullOrEmpty(translationResult.translation))
                {
                    var translatedLines = translationResult.translation.Split(SpecialSeparator);
                    var translatedLineIndex = 0;
                    // Count the number of translatable elements in the group
                    var numTranslatableElements = group.Count(d => (bool)d["translate"]);
                    if (translatedLines.Length != numTranslatableElements)
                    {
                        _translationProgressService.LogError(translateRequest.ProductId, translateRequest.ProductName,
                            $"Mismatch in translated lines and translatable elements count. Expected: {numTranslatableElements}, Received: {translatedLines.Length}");
                        // Skip translation for this group and preserve original content
                        foreach (var dict in group)
                        {
                            if ((bool)dict["translate"])
                            {
                                // Eğer translatedLineIndex, translatedLines dizisinin sınırları dışındaysa, hata yönetimi yapın
                                if (translatedLineIndex >= translatedLines.Length)
                                {
                                    _translationProgressService.LogError(translateRequest.ProductId,
                                        translateRequest.ProductName,
                                        "Index was outside the bounds of the array during translation.");
                                    // Hata durumunda ne yapılacağına karar verin (örneğin, orijinal içeriği kullanın veya bir hata mesajı gösterin)
                                    // Örnek: Orijinal içeriği kullanma
                                    continue;
                                }

                                // Tablo hücresi ise sadece içeriği güncelle
                                if (((string)dict["openTag"]).StartsWith("<td") ||
                                    ((string)dict["openTag"]).StartsWith("<th"))
                                {
                                    dict["content"] = translatedLines[translatedLineIndex++];
                                }
                                else
                                {
                                    UpdateNestedContent(dict["content"], translatedLines, ref translatedLineIndex);
                                }
                            }

                            // Her öğeyi translatedContent'e eklerken, önceki öğenin kapanış etiketi ile mevcut öğenin açılış etiketi arasında boşluk olup olmadığını kontrol et
                            if (translatedContent.Length > 0 &&
                                !char.IsWhiteSpace(translatedContent[^1]) &&
                                !((string)dict["openTag"]).StartsWith("<") &&
                                !((string)dict["closeTag"]).EndsWith(">"))
                            {
                                translatedContent.Append(" ");
                            }

                            translatedContent.Append(dict["openTag"]);
                            translatedContent.Append(ConvertContentToString(dict["content"])
                                .Replace(SpecialSeparator, Environment.NewLine));
                            translatedContent.Append(dict["closeTag"]);
                        }
                    }
                }
                    else
                {
                    _translationProgressService.LogError(translateRequest.ProductId, translateRequest.ProductName, $"Full Description translation failed for {translateRequest.Target}");
                    // Hata durumunda ne yapılacağına karar verin (örneğin, orijinal içeriği kullanın veya bir hata mesajı gösterin)
                    // Örnek: Orijinal içeriği kullanma
                    foreach (var dict in group)
                    {
                        translatedContent.Append(dict["openTag"]);
                        translatedContent.Append(ConvertContentToString(dict["content"]));
                        translatedContent.Append(dict["closeTag"]);
                    }
                }
            }

            return translatedContent.ToString();
        }
        private void UpdateNestedContent(object content, string[] translatedLines, ref int translatedLineIndex)
        {
            if (content is string)
            {
                // Metin içeriği ise, çevrilmiş satırı ata
                content = translatedLines[translatedLineIndex++];
            }
            else if (content is List<Dictionary<string, object>> childElements)
            {
                // İç içe dictionary yapısı ise, özyinelemeli olarak güncelle
                foreach (var childElement in childElements)
                {
                    UpdateNestedContent(childElement["content"], translatedLines, ref translatedLineIndex);
                }
            }
        }
        private string ConvertContentToString(object content)
        {
            if (content is string strContent)
            {
                return strContent;
            }
            else if (content is List<Dictionary<string, object>> childElements)
            {
                var stringBuilder = new StringBuilder();
                foreach (var childElement in childElements)
                {
                    // Eğer tablo hücresiyse sadece içeriği çevir
                    if (((string)childElement["openTag"]).StartsWith("<td") || ((string)childElement["openTag"]).StartsWith("<th"))
                    {
                        // İç içe geçmiş elemanlardan sadece metni çıkar
                        stringBuilder.Append(ExtractTextFromNestedElements(childElement["content"]));
                    }
                    else
                    {
                        stringBuilder.Append(childElement["openTag"]);
                        stringBuilder.Append(ConvertContentToString(childElement["content"]));
                        stringBuilder.Append(childElement["closeTag"]);
                    }
                }
                return stringBuilder.ToString();
            }
            return string.Empty;
        }
        private string ExtractTextFromNestedElements(object content)
        {
            if (content is string strContent)
            {
                return strContent;
            }
            else if (content is List<Dictionary<string, object>> childElements)
            {
                var stringBuilder = new StringBuilder();
                foreach (var childElement in childElements)
                {
                    // Sadece metin düğümlerinin içeriğini ekle
                    if (childElement["openTag"] is string openTag && string.IsNullOrEmpty(openTag))
                    {
                        stringBuilder.Append(childElement["content"]);
                    }
                    else
                    {
                        // İç içe geçmiş elemanlardan metni çıkar, ancak etiketleri dahil etme
                        stringBuilder.Append(ExtractTextFromNestedElements(childElement["content"]));
                    }
                }
                return stringBuilder.ToString();
            }
            return string.Empty;
        }
        private string GetAttributesString(HtmlNode node)
        {
            var attributes = node.Attributes.Select(attr => $"{attr.Name}=\"{attr.Value}\"");
            return attributes.Any() ? " " + string.Join(" ", attributes) : string.Empty;
        }

        private List<Dictionary<string, object>> ParseNode(HtmlNode node)
        {
            var htmlParts = new List<Dictionary<string, object>>();

            if (node.NodeType == HtmlNodeType.Element)
            {
                var htmlElement = new Dictionary<string, object>
        {
            { "openTag", $"<{node.Name}{GetAttributesString(node)}>" },
            { "closeTag", node.Name.Equals("img", StringComparison.OrdinalIgnoreCase) ||
                           node.Name.Equals("iframe", StringComparison.OrdinalIgnoreCase) ||
                           node.Name.Equals("video", StringComparison.OrdinalIgnoreCase) ||
                           HtmlNode.IsEmptyElement(node.Name) ? "" : $"</{node.Name}>" },
            { "translate", true } // Varsayılan olarak çevrilecek
        };

                if (node.HasChildNodes)
                {
                    var childParts = new List<Dictionary<string, object>>();
                    foreach (var child in node.ChildNodes)
                    {
                        // Tablo hücrelerini ve başlıklarını çeviriye dahil et
                        if (child.Name == "td" || child.Name == "th")
                        {
                            childParts.AddRange(ParseNode(child));
                        }
                        else
                        {
                            // Diğer tablo öğelerini (satır, gövde vb.) çeviriye dahil etme
                            var childDict = new Dictionary<string, object>
                    {
                        { "openTag", $"<{child.Name}{GetAttributesString(child)}>" },
                        { "closeTag", child.Name.Equals("img", StringComparison.OrdinalIgnoreCase) ||
                                       child.Name.Equals("iframe", StringComparison.OrdinalIgnoreCase) ||
                                       child.Name.Equals("video", StringComparison.OrdinalIgnoreCase) ||
                                       HtmlNode.IsEmptyElement(child.Name) ? "" : $"</{child.Name}>" },
                        { "translate", false }
                    };

                            if (child.HasChildNodes)
                            {
                                childDict["content"] = ParseNode(child);
                            }
                            else
                            {
                                if (child.NodeType == HtmlNodeType.Text)
                                {
                                    var textContent = child.InnerText.Trim(); // child düğümünün InnerText'ini kullan

                                    // Check if the text content is not empty or whitespace
                                    if (!string.IsNullOrWhiteSpace(textContent))
                                    {
                                        // Text düğümünün özelliklerini childDict'e ata
                                        childDict["openTag"] = "";
                                        childDict["closeTag"] = "";
                                        childDict["translate"] = true;
                                        childDict["content"] = textContent;
                                    }
                                    else
                                    {
                                        // Boş veya sadece boşluk içeren metin düğümlerini yok say
                                        continue;
                                    }
                                }
                                else
                                {
                                    childDict["content"] = child.InnerText;
                                }
                            }

                            childParts.Add(childDict);
                        }
                    }
                    htmlElement["content"] = childParts;
                }
                else
                {
                    htmlElement["content"] = node.InnerText;
                }

                // Çevirilmemesi gereken etiketler için "translate" değerini false yap
                if (node.Name == "img" || node.Name == "script" || node.Name == "style" ||
                    node.Name == "video" || node.Name == "meta" || node.Name == "iframe" ||
                    node.Name == "tr" || node.Name == "tbody")
                {
                    htmlElement["translate"] = false;
                }

                htmlParts.Add(htmlElement);
            }
            else if (node.NodeType == HtmlNodeType.Text)
            {
                var textContent = node.InnerText.Trim();

                // Check if the text content is not empty or whitespace
                if (!string.IsNullOrWhiteSpace(textContent))
                {
                    htmlParts.Add(new Dictionary<string, object>
            {
                { "openTag", "" },
                { "content", textContent },
                { "closeTag", "" },
                { "translate", true }
            });
                }
            }

            return htmlParts;
        }
        public async Task<List<Dictionary<string, object>>> ParseHtmlToList(string htmlContent)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(htmlContent);

            // DocumentNode'un altındaki ilk çocuk düğümden başlayarak ayrıştır
            var htmlParts = new List<Dictionary<string, object>>();
            if (doc.DocumentNode.HasChildNodes)
            {
                foreach (var child in doc.DocumentNode.ChildNodes)
                {
                    htmlParts.AddRange(ParseNode(child));
                }
            }

            return htmlParts;
        }

        private List<List<Dictionary<string, object>>> CreateTranslationGroups(List<Dictionary<string, object>> htmlParts)
        {
            var translationGroups = new List<List<Dictionary<string, object>>>();
            var currentGroup = new List<Dictionary<string, object>>();
            var currentChunkSize = 0;

            foreach (var element in htmlParts)
            {
                // Tüm içeriğin boyutunu hesapla (alt öğeler dahil)
                var elementSize = CalculateElementSize(element);

                if ((bool)element["translate"] && !string.IsNullOrWhiteSpace(ConvertContentToString(element["content"])))
                {
                    // Tablo hücrelerini ayrı bir grup olarak ele al
                    if (((string)element["openTag"]).StartsWith("<td") || ((string)element["openTag"]).StartsWith("<th"))
                    {
                        translationGroups.Add(new List<Dictionary<string, object>> { element });
                        continue;
                    }

                    if (currentChunkSize + elementSize > 4000 || elementSize > 4000)
                    {
                        translationGroups.Add(currentGroup);
                        currentGroup = new List<Dictionary<string, object>>();
                        currentChunkSize = 0;
                    }

                    currentGroup.Add(element);
                    currentChunkSize += elementSize;
                }
                else
                {
                    // Çevrilmeyecek öğeleri doğrudan yeni bir gruba ekleyin
                    translationGroups.Add(new List<Dictionary<string, object>> { element });
                }
            }

            if (currentGroup.Any())
            {
                translationGroups.Add(currentGroup);
            }

            return translationGroups;
        }

        private int CalculateElementSize(Dictionary<string, object> element)
        {
            var contentSize = ConvertContentToString(element["content"]).Length;
            var openTagSize = ((string)element["openTag"]).Length;
            var closeTagSize = ((string)element["closeTag"]).Length;
            return contentSize + openTagSize + closeTagSize;
        }


        #endregion

        #endregion
    }
}