
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

        #endregion

        #region Ctor

        public TranslateService(IProductService productService, ILocalizedEntityService localizedEntityService,
            IUrlRecordService urlRecordService,
            IProductModelFactory productModelFactory, ILanguageService languageService, IStoreContext storeContext)
        {
            _productService = productService;
            _localizedEntityService = localizedEntityService;
            _urlRecordService = urlRecordService;
            _productModelFactory = productModelFactory;
            _languageService = languageService;
            _storeContext = storeContext;

        }

        #endregion

        #region Methods
        #region MyRegion


        public async Task<TranslateResponse> Translate(TranslateRequest request)
        {
            TranslateResponse result = new TranslateResponse();
            var url = "http://localhost:3000/api/v1/translate";

            var requestBody = new { source = request.Source, target = request.Target, text = request.Text };

            var json = JsonConvert.SerializeObject(requestBody);

            var httpClient = new HttpClient();
            var response = await httpClient.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));
            if (!response.IsSuccessStatusCode)
            {
                return result;
            }

            var responseContent = await response.Content.ReadAsStringAsync();

            result = JsonConvert.DeserializeObject<TranslateResponse>(responseContent);


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



            int i = 0;
            foreach (var product in productsList)
            {
                i++;
                Console.WriteLine("%" + (((double)i / (double)productsList.Count) * 100).ToString("0.00"));

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
                            var result = await Translate(request);


                            if (!result.translation.IsNullOrEmpty())
                            {
                                var translatedName = result.translation;

                                request.Text = product.ShortDescription;
                                result = await Translate(request);

                                if (!result.translation.IsNullOrEmpty())
                                {
                                    var translatedShortDescription = result.translation;

                                    request.Text = product.FullDescription;
                                    result = await Translate(request);

                                    if (!result.translation.IsNullOrEmpty())
                                    {
                                        var translatedFullDescription = result.translation;

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