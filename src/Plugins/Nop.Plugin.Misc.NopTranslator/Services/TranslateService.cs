
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
using System.Xml.Linq;
using DocumentFormat.OpenXml.Presentation;
using HtmlAgilityPack;
using HtmlParserLibrary;
using Microsoft.AspNetCore.Mvc;
using Nop.Core.Domain.Localization;
using System.Web;
using Org.BouncyCastle.Asn1.X509;


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
                var parsedHtmlContent = await ParseHtmlToList(product.FullDescription);
                var translationGroups = parsedHtmlContent
                    .Select(element => new List<Dictionary<string, object>>
                    {
                        new Dictionary<string, object>
                        {
                            { "type", element.Type },
                            { "attributes", element.Attributes },
                            { "isEditable", element.IsEditable },
                            { "content", element.Content }
                        }
                    })
                    .ToList();

               
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
            var parser = new HtmlParser(); // Yeni HtmlParser sınıfını başlatın

            try
            {
                _translationProgressService.UpdateStartTime(DateTime.UtcNow);
                _translationProgressService.UpdateProgress(0, null);

                var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
                var activeLanguages = (await _languageService.GetAllLanguagesAsync(false, storeScope))
                                       .Where(x => x.Published)
                                       .ToList();

                var productsList = await _productService.SearchProductsAsync(
                    categoryIds: null,
                    showHidden: false,
                    storeId: storeScope
                );




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

                    // HTML içeriğini JSON olarak işleyin
                    var parsedJson = parser.ConvertHtmlToJson(HttpUtility.HtmlDecode(product.FullDescription));


                    var processedList = parser.ProcessJsonData(parsedJson);

                    var productModel = await _productModelFactory.PrepareProductModelAsync(new ProductModel(), product);

                    foreach (var language in activeLanguages)
                    {
                        var translatedChunks = new List<string>();

                        //var name = await _localizedEntityService.GetLocalizedValueAsync(language.Id, product.Id, "Product", "Name");
                        //var shortDescription = await _localizedEntityService.GetLocalizedValueAsync(language.Id, product.Id, "Product", "ShortDescription");
                        //var fullDescription = await _localizedEntityService.GetLocalizedValueAsync(language.Id, product.Id, "Product", "FullDescription");

                        var productModelLocale = productModel.Locales.SingleOrDefault(x => x.LanguageId == language.Id);
                        //if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(shortDescription) || string.IsNullOrEmpty(fullDescription))
                        if (productModelLocale != null &&
                        (productModelLocale.FullDescription.IsNullOrEmpty() ||
                         productModelLocale.ShortDescription.IsNullOrEmpty() ||
                         productModelLocale.Name.IsNullOrEmpty()))
                        {
                            needsTranslation = true;

                            // Çeviri işlemi
                            var translateRequest = new TranslateRequest
                            {
                                Source = "auto",
                                Target = language.LanguageCulture.Split('-')[0], // Örneğin "en"
                                Text = product.Name
                            };
                            if (translateRequest.Target == "nn")
                            {
                                translateRequest.Target = "no";
                            }
                            var translationResultName = await Translate(translateRequest);

                            if (!string.IsNullOrEmpty(translationResultName.translation))
                            {
                                //  await _localizedEntityService.SaveLocalizedValueAsync(product, p => p.Name, translationResultName.translation, language.Id);
                                translateRequest.Text = product.ShortDescription;

                                var translationResultShortDesc = await Translate(translateRequest);

                                if (!string.IsNullOrEmpty(translationResultShortDesc.translation))
                                {
                                    // await _localizedEntityService.SaveLocalizedValueAsync(product, p => p.ShortDescription, translationResultShortDesc.translation, language.Id);
                                    foreach (var text in processedList)
                                    {
                                        translateRequest.Text = text;
                                        var translationResultText = await Translate(translateRequest);

                                        if (!string.IsNullOrEmpty(translationResultText.translation))
                                        {
                                            translatedChunks.Add(translationResultText.translation);

                                        }
                                        else
                                        {
                                            _translationProgressService.LogError(product.Id, product.Name, $"Translation failed for {text}");
                                            continue; // Bir sonraki dile geç
                                        }

                                    }


                                    // Çevrilmiş JSON'u eski JSON'a update edin
                                    var editedChunks = parser.ParseEditedContent(string.Join("", translatedChunks));
                                    var updatedJson = parser.UpdateJsonWithEditedContent(parsedJson, editedChunks);

                                    var finalHtml = HttpUtility.HtmlDecode(parser.ConvertJsonToHtml(updatedJson));




                                    if (!string.IsNullOrEmpty(finalHtml))
                                    {
                                        //await _localizedEntityService.SaveLocalizedValueAsync(product, p => p.FullDescription, finalHtml, language.Id);
                                        productModelLocale.Name = translationResultName.translation;
                                        productModelLocale.ShortDescription = translationResultShortDesc.translation;
                                        productModelLocale.FullDescription = finalHtml;

                                        int index = productModel.Locales.IndexOf(productModelLocale);

                                        if (index != -1)
                                            productModel.Locales[index] = productModelLocale;
                                    }
                                    else
                                    {
                                        _translationProgressService.LogError(product.Id, product.Name, $"Full Description translation failed for {language.Name}");
                                        try
                                        {
                                            Console.WriteLine("NopTranslator Error:" + product.Id + "/" + product.Name + "/" + $"Full Description translation failed for {language.Name}");

                                        }
                                        catch
                                        {

                                        }
                                        continue; // Bir sonraki dile geç
                                    }
                                }
                                else
                                {
                                    _translationProgressService.LogError(product.Id, product.Name, $"Short Description translation failed for {language.Name}");
                                    continue; // Bir sonraki dile geç
                                }
                            }
                            else
                            {
                                _translationProgressService.LogError(product.Id, product.Name, $"Name translation failed for {language.Name}");
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

                    try
                    {
                        await UpdateLocalesAsync(product, productModel);

                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);

                    }
                }

                _translationProgressService.StopProgress();
                return translationResults;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Translation process failed: {ex.Message}");
                _translationProgressService.StopProgress();
            }

            return translationResults;
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
        public class HtmlElement
        {
            public string Type { get; set; }
            public Dictionary<string, string> Attributes { get; set; }
            public bool IsEditable { get; set; }
            public List<object> Content { get; set; } // İçerik string veya başka HTML elementleri olabilir
        }
        private bool IsEditableElement(XElement element)
        {
            string[] nonEditableTags = { "img", "video", "meta", "script", "style" };
            return !nonEditableTags.Contains(element.Name.LocalName.ToLower());
        }
        private HtmlElement ConvertNodeToHtmlElement(XElement element)
        {

            return new HtmlElement
            {
                Type = element.Name.LocalName,
                Attributes = element.Attributes().ToDictionary(attr => attr.Name.LocalName, attr => attr.Value),
                IsEditable = IsEditableElement(element),
                Content = element.Nodes().Select(node =>
                        node is XElement
                            ? (object)ConvertNodeToHtmlElement((XElement)node) // Alt elemanları da HtmlElement olarak dönüştür
                            : (object)node.ToString() // Text içerikler string olarak kalır
                ).ToList()
            };
        }

        public async Task<List<HtmlElement>> ParseHtmlToList(string htmlContent)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(htmlContent);

            var elements = doc.DocumentNode.Descendants()
                .Select(node => ConvertNodeToHtmlElement(XElement.Parse(node.OuterHtml)))
                .ToList();

            return elements;
        }




        #endregion

        #endregion
    }
}