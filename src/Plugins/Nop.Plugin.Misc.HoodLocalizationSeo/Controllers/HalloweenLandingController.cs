using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Plugin.Misc.HoodLocalizationSeo.Infrastructure;
using Nop.Plugin.Misc.HoodLocalizationSeo.Models;
using Nop.Services.Catalog;
using Nop.Services.Localization;
using Nop.Services.Seo;
using Nop.Web.Controllers;
using Nop.Web.Factories;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Controllers;

public sealed class HalloweenLandingController : BasePublicController
{
    // These are storefront categories already present in the Hood catalog.
    // Matching by the default name avoids inventing a seasonal category or a
    // synthetic product collection.
    private static readonly string[] RelatedCategoryNames =
    [
        "Medieval Clothing",
        "Hats/Helmets",
        "Medieval Weapons",
        "Archery"
    ];

    private readonly ICategoryService _categoryService;
    private readonly ILanguageService _languageService;
    private readonly ILocalizationService _localizationService;
    private readonly IProductModelFactory _productModelFactory;
    private readonly IProductService _productService;
    private readonly IStoreContext _storeContext;
    private readonly IUrlRecordService _urlRecordService;

    public HalloweenLandingController(ICategoryService categoryService,
        ILanguageService languageService,
        ILocalizationService localizationService,
        IProductModelFactory productModelFactory,
        IProductService productService,
        IStoreContext storeContext,
        IUrlRecordService urlRecordService)
    {
        _categoryService = categoryService;
        _languageService = languageService;
        _localizationService = localizationService;
        _productModelFactory = productModelFactory;
        _productService = productService;
        _storeContext = storeContext;
        _urlRecordService = urlRecordService;
    }

    [HttpGet]
    [CheckLanguageSeoCode(ignore: true)]
    public async Task<IActionResult> HalloweenLanding(string language)
    {
        var store = await _storeContext.GetCurrentStoreAsync();
        if (!TryGetCanonicalStoreOrigin(store.Url, out var canonicalOrigin))
            return NotFound();

        var languages = await _languageService.GetAllLanguagesAsync(storeId: store.Id);
        var targets = HalloweenLandingRoute.BuildTargets(canonicalOrigin, languages,
            store.DefaultLanguageId);
        var defaultTarget = targets.SingleOrDefault(target => target.IsDefault);
        if (defaultTarget is null)
            return NotFound();

        var requestedTarget = targets.FirstOrDefault(target =>
            target.LanguageCode.Equals(language, StringComparison.OrdinalIgnoreCase));
        if (requestedTarget is null ||
            !requestedTarget.LanguageCode.Equals(language, StringComparison.Ordinal))
        {
            return RedirectPermanent((requestedTarget ?? defaultTarget).Url);
        }

        var requestedLanguage = requestedTarget.Language;
        var categories = (await _categoryService.GetAllCategoriesAsync(store.Id))
            .Where(category => category.Published && !category.Deleted &&
                RelatedCategoryNames.Contains(category.Name, StringComparer.OrdinalIgnoreCase))
            .OrderBy(category => Array.FindIndex(RelatedCategoryNames,
                name => name.Equals(category.Name, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var categoryModels = new List<HalloweenLandingCategoryModel>();
        foreach (var category in categories)
        {
            var slug = await _urlRecordService.GetSeNameAsync(category.Id, nameof(Category), requestedLanguage.Id,
                returnDefaultValue: true, ensureTwoPublishedLanguages: false);
            if (string.IsNullOrWhiteSpace(slug))
                continue;

            var name = await _localizationService.GetLocalizedAsync(category, entity => entity.Name,
                requestedLanguage.Id, ensureTwoPublishedLanguages: false);
            categoryModels.Add(new HalloweenLandingCategoryModel(name,
                BuildLocalizedPath(canonicalOrigin, requestedLanguage.UniqueSeoCode, slug)));
        }

        var products = categories.Count == 0
            ? Array.Empty<Product>()
            : (await _productService.SearchProductsAsync(pageIndex: 0, pageSize: 12,
                categoryIds: categories.Select(category => category.Id).ToList(), storeId: store.Id,
                visibleIndividuallyOnly: true, languageId: requestedLanguage.Id)).ToArray();
        var productModels = (await _productModelFactory.PrepareProductOverviewModelsAsync(products)).ToList();

        var text = HalloweenLandingText.For(requestedLanguage.UniqueSeoCode);
        var model = new HalloweenLandingModel
        {
            Title = text.Title,
            MetaDescription = text.MetaDescription,
            Introduction = text.Introduction,
            CategoriesHeading = text.CategoriesHeading,
            ProductsHeading = text.ProductsHeading,
            Categories = categoryModels,
            Products = productModels
        };

        return View("~/Plugins/Nop.Plugin.Misc.HoodLocalizationSeo/Views/HalloweenLanding.cshtml", model);
    }

    private static string BuildLocalizedPath(Uri canonicalOrigin, string languageCode, string slug)
    {
        var pathBase = canonicalOrigin.AbsolutePath.TrimEnd('/');
        return $"{pathBase}/{Uri.EscapeDataString(languageCode)}/{Uri.EscapeDataString(slug)}";
    }

    private static bool TryGetCanonicalStoreOrigin(string storeUrl, out Uri canonicalOrigin)
    {
        canonicalOrigin = null;
        if (!Uri.TryCreate(storeUrl, UriKind.Absolute, out var storeUri) ||
            (storeUri.Scheme != Uri.UriSchemeHttp && storeUri.Scheme != Uri.UriSchemeHttps) ||
            string.IsNullOrWhiteSpace(storeUri.Host) ||
            !string.IsNullOrEmpty(storeUri.UserInfo) ||
            !string.IsNullOrEmpty(storeUri.Query) ||
            !string.IsNullOrEmpty(storeUri.Fragment))
        {
            return false;
        }

        return Uri.TryCreate(storeUri.GetLeftPart(UriPartial.Path).TrimEnd('/') + "/",
            UriKind.Absolute, out canonicalOrigin);
    }

    private sealed record HalloweenLandingText(string Title, string MetaDescription, string Introduction,
        string CategoriesHeading, string ProductsHeading)
    {
        public static HalloweenLandingText For(string languageCode) => languageCode?.ToLowerInvariant() switch
        {
            "tr" => new("Cadılar Bayramı okçuluk ve kostüm fikirleri",
                "Cadılar Bayramı, LARP ve temalı etkinlikler için ilgili okçuluk, kostüm ve aksesuar kategorilerini keşfedin.",
                "Cadılar Bayramı kostümleri, LARP ve temalı etkinlikler için başlangıç noktası. Malzeme, beden ve stok bilgileri için ürün sayfalarını inceleyin.",
                "İlgili kategorileri keşfedin", "Bu kategorilerden seçilen ürünler"),
            "de" => new("Halloween-Ideen für Bogensport und Kostüme",
                "Entdecken Sie passende Kategorien für Halloween, LARP und thematische Veranstaltungen.",
                "Ein praktischer Ausgangspunkt für Halloween-Kostüme, LARP und thematische Veranstaltungen. Prüfen Sie Material, Größen und Verfügbarkeit auf den jeweiligen Produktseiten.",
                "Passende Kategorien", "Ausgewählte Produkte aus diesen Kategorien"),
            "fr" => new("Idées Halloween : archerie et costumes",
                "Découvrez des catégories adaptées à Halloween, au GN et aux événements à thème.",
                "Un point de départ pratique pour les costumes d’Halloween, le GN et les événements à thème. Consultez chaque fiche produit pour les matières, les tailles et la disponibilité.",
                "Explorer les catégories associées", "Produits sélectionnés dans ces catégories"),
            "es" => new("Ideas de Halloween: arquería y disfraces",
                "Descubre categorías relacionadas con Halloween, LARP y eventos temáticos.",
                "Un punto de partida práctico para disfraces de Halloween, LARP y eventos temáticos. Consulta cada página de producto para conocer materiales, tallas y disponibilidad.",
                "Explorar categorías relacionadas", "Productos seleccionados de estas categorías"),
            "en" => new("Halloween archery and costume ideas",
                "Explore related archery, costume and accessory categories for Halloween, LARP and themed events.",
                "A practical starting point for Halloween costumes, LARP and themed events. Check individual product pages for materials, sizing and availability.",
                "Explore related categories", "Selected products from these categories"),
            _ => throw new ArgumentOutOfRangeException(nameof(languageCode), languageCode,
                "The Halloween landing has no translated copy for this language.")
        };
    }
}
