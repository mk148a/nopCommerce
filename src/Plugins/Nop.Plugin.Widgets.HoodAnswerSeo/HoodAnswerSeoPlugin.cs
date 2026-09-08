using Nop.Services.Cms;
using Nop.Core.Domain.Cms;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Plugins;
using Nop.Web.Framework.Infrastructure;

namespace Nop.Plugin.Widgets.HoodAnswerSeo;

/// <summary>
/// Adds a concise, visible answer surface to individual product pages without replacing core templates.
/// </summary>
public sealed class HoodAnswerSeoPlugin : BasePlugin, IWidgetPlugin
{
    private const string ResourcePrefix = "Plugins.Widgets.HoodAnswerSeo.";
    private static readonly string[] ResourceNames =
    [
        "ProductFacts", "WhatItIs", "Category", "Sku", "Availability", "Shipping",
        "CustomerRating", "WhatCustomersSay", "FreeShipping", "AnonymousCustomer"
    ];

    private readonly ILocalizationService _localizationService;
    private readonly ILanguageService _languageService;
    private readonly WidgetSettings _widgetSettings;
    private readonly ISettingService _settingService;

    public HoodAnswerSeoPlugin(ILocalizationService localizationService, ILanguageService languageService,
        WidgetSettings widgetSettings, ISettingService settingService)
    {
        _localizationService = localizationService;
        _languageService = languageService;
        _widgetSettings = widgetSettings;
        _settingService = settingService;
    }

    public bool HideInWidgetList => false;

    public Task<IList<string>> GetWidgetZonesAsync()
        // Keep this outside the two-column product-essential layout. It is intentionally
        // a compact fact strip, not a second product description or review list.
        => Task.FromResult<IList<string>>([PublicWidgetZones.ProductDetailsBeforeCollateral]);

    public Type GetWidgetViewComponent(string widgetZone)
    {
        ArgumentNullException.ThrowIfNull(widgetZone);
        return typeof(Components.HoodAnswerSeoViewComponent);
    }

    public override async Task InstallAsync()
    {
        if (!_widgetSettings.ActiveWidgetSystemNames.Contains(PluginDescriptor.SystemName))
        {
            _widgetSettings.ActiveWidgetSystemNames.Add(PluginDescriptor.SystemName);
            await _settingService.SaveSettingAsync(_widgetSettings);
        }

        foreach (var language in await _languageService.GetAllLanguagesAsync(true))
        {
            var languageCode = language.LanguageCulture?.Split('-', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault()?.ToLowerInvariant() ?? "en";
            await _localizationService.AddOrUpdateLocaleResourceAsync(GetResources(languageCode), language.Id);
        }

        await base.InstallAsync();
    }

    public override async Task UninstallAsync()
    {
        if (_widgetSettings.ActiveWidgetSystemNames.Contains(PluginDescriptor.SystemName))
        {
            _widgetSettings.ActiveWidgetSystemNames.Remove(PluginDescriptor.SystemName);
            await _settingService.SaveSettingAsync(_widgetSettings);
        }

        foreach (var resourceName in ResourceNames)
            await _localizationService.DeleteLocaleResourceAsync($"{ResourcePrefix}{resourceName}");

        await base.UninstallAsync();
    }

    private static Dictionary<string, string> GetResources(string languageCode)
    {
        string[] values = languageCode switch
        {
            "tr" => ["Ürün bilgileri", "Nedir", "Kategori", "Stok kodu", "Stok durumu", "Kargo", "Müşteri puanı", "Müşteriler ne diyor", "Ücretsiz kargo", "Müşteri"],
            "de" => ["Produktinformationen", "Was ist es?", "Kategorie", "Artikelnummer", "Verfügbarkeit", "Versand", "Kundenbewertung", "Das sagen Kunden", "Kostenloser Versand", "Kunde"],
            "fr" => ["Informations produit", "Description", "Catégorie", "Référence", "Disponibilité", "Livraison", "Note des clients", "Ce que disent les clients", "Livraison gratuite", "Client"],
            "es" => ["Información del producto", "Qué es", "Categoría", "Referencia", "Disponibilidad", "Envío", "Valoración de clientes", "Opiniones de clientes", "Envío gratis", "Cliente"],
            "it" => ["Informazioni sul prodotto", "Che cos'è", "Categoria", "Codice prodotto", "Disponibilità", "Spedizione", "Valutazione clienti", "Cosa dicono i clienti", "Spedizione gratuita", "Cliente"],
            "pt" => ["Informações do produto", "O que é", "Categoria", "Referência", "Disponibilidade", "Envio", "Avaliação dos clientes", "O que dizem os clientes", "Envio gratuito", "Cliente"],
            "nl" => ["Productinformatie", "Wat is het?", "Categorie", "Artikelnummer", "Beschikbaarheid", "Verzending", "Klantbeoordeling", "Wat klanten zeggen", "Gratis verzending", "Klant"],
            "da" => ["Produktoplysninger", "Hvad er det?", "Kategori", "Varenummer", "Tilgængelighed", "Levering", "Kundevurdering", "Hvad kunder siger", "Gratis levering", "Kunde"],
            "hu" => ["Termékinformációk", "Mi ez?", "Kategória", "Cikkszám", "Elérhetőség", "Szállítás", "Vásárlói értékelés", "Mit mondanak a vásárlók", "Ingyenes szállítás", "Vásárló"],
            "no" or "nn" => ["Produktinformasjon", "Hva er det?", "Kategori", "Varenummer", "Tilgjengelighet", "Frakt", "Kundevurdering", "Hva kundene sier", "Gratis frakt", "Kunde"],
            "pl" => ["Informacje o produkcie", "Co to jest?", "Kategoria", "SKU", "Dostępność", "Dostawa", "Ocena klientów", "Co mówią klienci", "Darmowa dostawa", "Klient"],
            "ro" => ["Informații despre produs", "Ce este", "Categorie", "Cod produs", "Disponibilitate", "Livrare", "Evaluarea clienților", "Ce spun clienții", "Livrare gratuită", "Client"],
            "sv" => ["Produktinformation", "Vad är det?", "Kategori", "Artikelnummer", "Tillgänglighet", "Leverans", "Kundbetyg", "Vad kunder säger", "Fri frakt", "Kund"],
            "el" => ["Πληροφορίες προϊόντος", "Τι είναι", "Κατηγορία", "Κωδικός προϊόντος", "Διαθεσιμότητα", "Αποστολή", "Βαθμολογία πελατών", "Τι λένε οι πελάτες", "Δωρεάν αποστολή", "Πελάτης"],
            "ms" => ["Maklumat produk", "Apakah ini", "Kategori", "SKU", "Ketersediaan", "Penghantaran", "Penilaian pelanggan", "Apa kata pelanggan", "Penghantaran percuma", "Pelanggan"],
            "ru" => ["Информация о товаре", "Что это", "Категория", "Артикул", "Наличие", "Доставка", "Оценка покупателей", "Что говорят покупатели", "Бесплатная доставка", "Покупатель"],
            "uk" => ["Інформація про товар", "Що це", "Категорія", "Артикул", "Наявність", "Доставка", "Оцінка покупців", "Що кажуть покупці", "Безкоштовна доставка", "Покупець"],
            "ar" => ["معلومات المنتج", "ما هو", "الفئة", "رمز المنتج", "التوفر", "الشحن", "تقييم العملاء", "ماذا يقول العملاء", "شحن مجاني", "عميل"],
            "ur" => ["مصنوعات کی معلومات", "یہ کیا ہے", "زمرہ", "پروڈکٹ کوڈ", "دستیابی", "ترسیل", "صارف کی درجہ بندی", "صارفین کیا کہتے ہیں", "مفت ترسیل", "صارف"],
            "ja" => ["商品情報", "商品について", "カテゴリー", "SKU", "在庫状況", "配送", "顧客評価", "お客様の声", "送料無料", "お客様"],
            "zh" => ["产品信息", "是什么", "类别", "SKU", "库存情况", "配送", "客户评分", "客户评价", "免费配送", "客户"],
            "ko" => ["상품 정보", "상품 소개", "카테고리", "SKU", "재고 현황", "배송", "고객 평점", "고객 후기", "무료 배송", "고객"],
            _ => ["Product facts", "What it is", "Category", "SKU", "Availability", "Shipping", "Customer rating", "What customers say", "Free shipping", "Customer"]
        };

        return ResourceNames.Select((resourceName, index) => new KeyValuePair<string, string>($"{ResourcePrefix}{resourceName}", values[index]))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
    }
}
