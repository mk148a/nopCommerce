using System.Globalization;
using System.Text.RegularExpressions;
using Nop.Core;
using Nop.Core.Domain.Shipping;
using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.Shipping.FixedByWeightByTotal.Components;
using Nop.Plugin.Shipping.FixedByWeightByTotal.Domain;
using Nop.Plugin.Shipping.FixedByWeightByTotal.Services;
using Nop.Plugin.Shipping.FixedByWeightByTotal.Services.ShippingDimensions;
using Nop.Plugin.Shipping.FixedByWeightByTotal.Services.ProductionTime;
using Nop.Services.Cms;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Plugins;
using Nop.Services.Shipping;
using Nop.Services.Shipping.Tracking;
using Nop.Web.Framework.Infrastructure;

namespace Nop.Plugin.Shipping.FixedByWeightByTotal;

/// <summary>
/// Fixed rate or by weight shipping computation method 
/// </summary>
public class FixedByWeightByTotalComputationMethod : BasePlugin, IShippingRateComputationMethod, IWidgetPlugin
{
    #region Fields

    protected readonly FixedByWeightByTotalSettings _fixedByWeightByTotalSettings;
    protected readonly ILocalizationService _localizationService;
    protected readonly IShoppingCartService _shoppingCartService;
    protected readonly ISettingService _settingService;
    protected readonly IShippingByWeightByTotalService _shippingByWeightByTotalService;
    protected readonly IProductShippingDimensionService _productShippingDimensionService;
    protected readonly IProductProductionTimeService _productProductionTimeService;
    protected readonly IShippingService _shippingService;
    protected readonly IStoreContext _storeContext;
    protected readonly IWebHelper _webHelper;

    #endregion

    #region Ctor

    public FixedByWeightByTotalComputationMethod(FixedByWeightByTotalSettings fixedByWeightByTotalSettings,
        ILocalizationService localizationService,
        IShoppingCartService shoppingCartService,
        ISettingService settingService,
        IShippingByWeightByTotalService shippingByWeightByTotalService,
        IProductShippingDimensionService productShippingDimensionService,
        IProductProductionTimeService productProductionTimeService,
        IShippingService shippingService,
        IStoreContext storeContext,
        IWebHelper webHelper)
    {
        _fixedByWeightByTotalSettings = fixedByWeightByTotalSettings;
        _localizationService = localizationService;
        _shoppingCartService = shoppingCartService;
        _settingService = settingService;
        _shippingByWeightByTotalService = shippingByWeightByTotalService;
        _productShippingDimensionService = productShippingDimensionService;
        _productProductionTimeService = productProductionTimeService;
        _shippingService = shippingService;
        _storeContext = storeContext;
        _webHelper = webHelper;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Get fixed rate
    /// </summary>
    /// <param name="shippingMethodId">Shipping method ID</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the rate
    /// </returns>
    protected async Task<decimal> GetRateAsync(int shippingMethodId)
    {
        return await _settingService.GetSettingByKeyAsync<decimal>(string.Format(FixedByWeightByTotalDefaults.FIXED_RATE_SETTINGS_KEY, shippingMethodId));
    }

    /// <summary>
    /// Gets the transit days
    /// </summary>
    /// <param name="shippingMethodId">Shipping method ID</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the ransit days
    /// </returns>
    protected async Task<int?> GetTransitDaysAsync(int shippingMethodId)
    {
        return await _settingService.GetSettingByKeyAsync<int?>(string.Format(FixedByWeightByTotalDefaults.TRANSIT_DAYS_SETTINGS_KEY, shippingMethodId));
    }

    protected static string NormalizePublicShippingMethodName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var cleaned = Regex.Replace(name, @"^\s*NAVLUNGO\s+", string.Empty, RegexOptions.IgnoreCase).Trim();
        cleaned = Regex.Replace(cleaned, @"\s+", " ");

        var words = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < words.Length; i++)
        {
            var token = words[i].Trim();
            var upper = token.ToUpperInvariant();
            words[i] = upper switch
            {
                "UPS" => "UPS",
                "FEDEX" => "FedEx",
                "DHL" => "DHL",
                "USPS" => "USPS",
                "TNT" => "TNT",
                "THY" => "THY",
                "PTT" => "PTT",
                "DPD" => "DPD",
                "GLS" => "GLS",
                "EXPRESS" => "Express",
                "EXPEDITED" => "Expedited",
                "ECONOMY" => "Economy",
                "STANDARD" => "Standard",
                "PRIORITY" => "Priority",
                _ => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(token.ToLowerInvariant())
            };
        }

        return string.Join(' ', words).Trim();
    }

    protected static string FormatDays(int minDays, int maxDays)
    {
        minDays = Math.Max(0, minDays);
        maxDays = Math.Max(minDays, maxDays);

        if (maxDays <= 0)
            return string.Empty;

        if (minDays <= 0 || minDays == maxDays)
            return maxDays == 1 ? "1 day" : $"{maxDays} days";

        return $"{minDays}-{maxDays} days";
    }

    protected static string FormatEstimatedDeliveryDateRange(int minDaysFromToday, int maxDaysFromToday)
    {
        minDaysFromToday = Math.Max(0, minDaysFromToday);
        maxDaysFromToday = Math.Max(minDaysFromToday, maxDaysFromToday);

        // Use the web server's local date as the order date baseline. This matches the store/admin date context
        // without adding a new nopCommerce service dependency to the shipping plugin constructor.
        var start = DateTime.Today.AddDays(minDaysFromToday);
        var end = DateTime.Today.AddDays(maxDaysFromToday);
        var culture = CultureInfo.CurrentUICulture;

        if (start.Date == end.Date)
            return start.ToString("MMM d, yyyy", culture);

        if (start.Year == end.Year && start.Month == end.Month)
            return $"{start.ToString("MMM d", culture)} - {end.ToString("d, yyyy", culture)}";

        if (start.Year == end.Year)
            return $"{start.ToString("MMM d", culture)} - {end.ToString("MMM d, yyyy", culture)}";

        return $"{start.ToString("MMM d, yyyy", culture)} - {end.ToString("MMM d, yyyy", culture)}";
    }

    protected static string BuildShippingOptionDescription(int? transitDays, (int MinDays, int MaxDays) productionRange)
    {
        var parts = new List<string>();

        if (transitDays.HasValue && transitDays.Value > 0)
            parts.Add($"Transit time: {FormatDays(transitDays.Value, transitDays.Value)}");

        if (productionRange.MaxDays > 0)
        {
            parts.Add($"Production time: {FormatDays(productionRange.MinDays, productionRange.MaxDays)}");

            if (transitDays.HasValue && transitDays.Value > 0)
            {
                var estimatedMin = Math.Max(0, productionRange.MinDays) + transitDays.Value;
                var estimatedMax = Math.Max(productionRange.MaxDays, productionRange.MinDays) + transitDays.Value;
                parts.Add($"Estimated delivery date: {FormatEstimatedDeliveryDateRange(estimatedMin, estimatedMax)}");
            }
            else
            {
                parts.Add("Estimated delivery date: calculated after shipping method confirmation");
            }

            parts.Add("Shipping starts after production.");
        }

        return string.Join("<br />", parts);
    }

    /// <summary>
    /// Get rate by weight and by total
    /// </summary>
    /// <param name="shippingByWeightByTotalRecord">Shipping by weight/by total record</param>
    /// <param name="subTotal">Subtotal</param>
    /// <param name="weight">Weight</param>
    /// <returns>Rate</returns>
    protected decimal GetRate(ShippingByWeightByTotalRecord shippingByWeightByTotalRecord, decimal subTotal, decimal weight)
    {
        //additional fixed cost
        var shippingTotal = shippingByWeightByTotalRecord.AdditionalFixedCost;

        //charge amount per weight unit
        if (shippingByWeightByTotalRecord.RatePerWeightUnit > decimal.Zero)
        {
            var weightRate = Math.Max(weight - shippingByWeightByTotalRecord.LowerWeightLimit, decimal.Zero);
            shippingTotal += shippingByWeightByTotalRecord.RatePerWeightUnit * weightRate;
        }

        //percentage rate of subtotal
        if (shippingByWeightByTotalRecord.PercentageRateOfSubtotal > decimal.Zero)
        {
            shippingTotal += Math.Round((decimal)((((float)subTotal) * ((float)shippingByWeightByTotalRecord.PercentageRateOfSubtotal)) / 100f), 2);
        }

        return Math.Max(shippingTotal, decimal.Zero);
    }


    /// <summary>
    /// Calculates effective rate-table weight using product/attribute shipping dimension rules.
    /// If no rule exists for an item, native nopCommerce product dimensions and weight adjustments are used.
    /// </summary>
    protected async Task<decimal> GetHoodNavlungoChargeableWeightAsync(GetShippingOptionRequest getShippingOptionRequest)
    {
        var total = decimal.Zero;

        foreach (var packageItem in getShippingOptionRequest.Items)
        {
            if (await _shippingService.IsFreeShippingAsync(packageItem.ShoppingCartItem))
                continue;

            var measure = await _productShippingDimensionService.GetCartItemMeasureAsync(packageItem.ShoppingCartItem);
            total += measure.RateLookupWeight;
        }

        return total;
    }

    #endregion

    #region Methods

    /// <summary>
    ///  Gets available shipping options
    /// </summary>
    /// <param name="getShippingOptionRequest">A request for getting shipping options</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the represents a response of getting shipping rate options
    /// </returns>
    public async Task<GetShippingOptionResponse> GetShippingOptionsAsync(GetShippingOptionRequest getShippingOptionRequest)
    {
        ArgumentNullException.ThrowIfNull(getShippingOptionRequest);

        var response = new GetShippingOptionResponse();

        if (getShippingOptionRequest.Items == null || !getShippingOptionRequest.Items.Any())
        {
            response.AddError("No shipment items");
            return response;
        }

        //choose the shipping rate calculation method
        if (_fixedByWeightByTotalSettings.ShippingByWeightByTotalEnabled)
        {
            //shipping rate calculation by products weight

            if (getShippingOptionRequest.ShippingAddress == null)
            {
                response.AddError("Shipping address is not set");
                return response;
            }

            var store = await _storeContext.GetCurrentStoreAsync();
            var storeId = getShippingOptionRequest.StoreId != 0 ? getShippingOptionRequest.StoreId : store.Id;
            var countryId = getShippingOptionRequest.ShippingAddress.CountryId ?? 0;
            var stateProvinceId = getShippingOptionRequest.ShippingAddress.StateProvinceId ?? 0;
            var warehouseId = getShippingOptionRequest.WarehouseFrom?.Id ?? 0;
            var zip = getShippingOptionRequest.ShippingAddress.ZipPostalCode;

            //get subtotal of shipped items
            var subTotal = decimal.Zero;
            foreach (var packageItem in getShippingOptionRequest.Items)
            {
                if (await _shippingService.IsFreeShippingAsync(packageItem.ShoppingCartItem))
                    continue;

                subTotal += (await _shoppingCartService.GetSubTotalAsync(packageItem.ShoppingCartItem, true)).subTotal;
            }

            //get chargeable weight of shipped items (excluding items with free shipping).
            //Hood/Navlungo fix: calculate max(actual weight, dimensional weight) per selected attribute values,
            //then pass that effective weight to the existing FixedByWeightByTotal rate table.
            var weight = _fixedByWeightByTotalSettings.HoodNavlungoChargeableWeightEnabled
                ? await GetHoodNavlungoChargeableWeightAsync(getShippingOptionRequest)
                : await _shippingService.GetTotalWeightAsync(getShippingOptionRequest, ignoreFreeShippedItems: true);

            // Handmade / made-to-order products require production time before carrier transit starts.
            // Add the longest production lead time in the package to the returned transit days so product-page
            // estimates and checkout delivery dates do not show carrier transit alone.
            var productionRange = await _productProductionTimeService.GetProductionDayRangeForCartItemsAsync(
                getShippingOptionRequest.Items.Select(i => i.ShoppingCartItem));
            var productionMaxDays = productionRange.MaxDays;

            foreach (var shippingMethod in await _shippingService.GetAllShippingMethodsAsync(countryId))
            {
                int? transitDays = null;
                var rate = decimal.Zero;

                var shippingByWeightByTotalRecord = await _shippingByWeightByTotalService.FindRecordsAsync(
                    shippingMethod.Id, storeId, warehouseId, countryId, stateProvinceId, zip, weight, subTotal);
                if (shippingByWeightByTotalRecord == null)
                {
                    // Hood/Navlungo fix:
                    // Do not show shipping methods without a configured matching rate.
                    // The original plugin could show unconfigured methods as 0.00 when
                    // LimitMethodsToCreated was disabled. For rate tables imported per
                    // country/weight/carrier, that lets customers choose unavailable
                    // carriers. A method must have an actual matching record to be offered.
                    continue;
                }

                rate = GetRate(shippingByWeightByTotalRecord, subTotal, weight);
                transitDays = shippingByWeightByTotalRecord.TransitDays;
                if (productionMaxDays > 0)
                    transitDays = (transitDays ?? 0) + productionMaxDays;

                var publicMethodName = NormalizePublicShippingMethodName(await _localizationService.GetLocalizedAsync(shippingMethod, x => x.Name));
                var methodDescription = BuildShippingOptionDescription(shippingByWeightByTotalRecord.TransitDays, productionRange);
                var adminDescription = await _localizationService.GetLocalizedAsync(shippingMethod, x => x.Description);
                if (!string.IsNullOrWhiteSpace(adminDescription))
                    methodDescription = string.IsNullOrWhiteSpace(methodDescription) ? adminDescription : methodDescription + "<br />" + adminDescription;

                response.ShippingOptions.Add(new ShippingOption
                {
                    Name = publicMethodName,
                    Description = methodDescription,
                    Rate = rate,
                    TransitDays = transitDays
                });
            }
        }
        else
        {
            //shipping rate calculation by fixed rate
            var restrictByCountryId = getShippingOptionRequest.ShippingAddress?.CountryId;
            var productionRange = await _productProductionTimeService.GetProductionDayRangeForCartItemsAsync(
                getShippingOptionRequest.Items.Select(i => i.ShoppingCartItem));

            response.ShippingOptions = await (await _shippingService.GetAllShippingMethodsAsync(restrictByCountryId)).SelectAwait(async shippingMethod =>
            {
                var carrierTransitDays = await GetTransitDaysAsync(shippingMethod.Id);
                var transitDays = carrierTransitDays;
                if (productionRange.MaxDays > 0)
                    transitDays = (transitDays ?? 0) + productionRange.MaxDays;

                var methodDescription = BuildShippingOptionDescription(carrierTransitDays, productionRange);
                var adminDescription = await _localizationService.GetLocalizedAsync(shippingMethod, x => x.Description);
                if (!string.IsNullOrWhiteSpace(adminDescription))
                    methodDescription = string.IsNullOrWhiteSpace(methodDescription) ? adminDescription : methodDescription + "<br />" + adminDescription;

                return new ShippingOption
                {
                    Name = NormalizePublicShippingMethodName(await _localizationService.GetLocalizedAsync(shippingMethod, x => x.Name)),
                    Description = methodDescription,
                    Rate = await GetRateAsync(shippingMethod.Id),
                    TransitDays = transitDays
                };
            }).ToListAsync();
        }

        return response;
    }

    /// <summary>
    /// Gets fixed shipping rate (if shipping rate computation method allows it and the rate can be calculated before checkout).
    /// </summary>
    /// <param name="getShippingOptionRequest">A request for getting shipping options</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the fixed shipping rate; or null in case there's no fixed shipping rate
    /// </returns>
    public async Task<decimal?> GetFixedRateAsync(GetShippingOptionRequest getShippingOptionRequest)
    {
        ArgumentNullException.ThrowIfNull(getShippingOptionRequest);

        //if the "shipping calculation by weight" method is selected, the fixed rate isn't calculated
        if (_fixedByWeightByTotalSettings.ShippingByWeightByTotalEnabled)
            return null;

        var restrictByCountryId = getShippingOptionRequest.ShippingAddress?.CountryId;
        var rates = await (await _shippingService.GetAllShippingMethodsAsync(restrictByCountryId))
            .SelectAwait(async shippingMethod => await GetRateAsync(shippingMethod.Id)).Distinct().ToListAsync();

        //return default rate if all of them equal
        if (rates.Count == 1)
            return rates.FirstOrDefault();

        return null;
    }

    /// <summary>
    /// Get associated shipment tracker
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the shipment tracker
    /// </returns>
    public Task<IShipmentTracker> GetShipmentTrackerAsync()
    {
        return Task.FromResult<IShipmentTracker>(new NavlungoShipmentTracker());
    }

    /// <summary>
    /// Gets a configuration page URL
    /// </summary>
    public override string GetConfigurationPageUrl()
    {
        return $"{_webHelper.GetStoreLocation()}Admin/FixedByWeightByTotal/Configure";
    }

    /// <summary>
    /// Install plugin
    /// </summary>
    /// <returns>A task that represents the asynchronous operation</returns>
    public override async Task InstallAsync()
    {
        //settings
        await _settingService.SaveSettingAsync(new FixedByWeightByTotalSettings
        {
            LoadAllRecord = true,
            ShippingByWeightByTotalEnabled = true,
            LimitMethodsToCreated = true,
        });

        //locales
        await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
        {
            ["Plugins.Shipping.FixedByWeightByTotal.AddRecord"] = "Add record",
            ["Plugins.Shipping.FixedByWeightByTotal.Fields.AdditionalFixedCost"] = "Additional fixed cost",
            ["Plugins.Shipping.FixedByWeightByTotal.Fields.AdditionalFixedCost.Hint"] = "Specify an additional fixed cost per shopping cart for this option. Set to 0 if you don't want an additional fixed cost to be applied.",
            ["Plugins.Shipping.FixedByWeightByTotal.Fields.Country"] = "Country",
            ["Plugins.Shipping.FixedByWeightByTotal.Fields.Country.Hint"] = "If an asterisk is selected, then this shipping rate will apply to all customers, regardless of the country.",
            ["Plugins.Shipping.FixedByWeightByTotal.Fields.DataHtml"] = "Data",
            ["Plugins.Shipping.FixedByWeightByTotal.Fields.LimitMethodsToCreated"] = "Limit shipping methods to configured ones",
            ["Plugins.Shipping.FixedByWeightByTotal.Fields.LimitMethodsToCreated.Hint"] = "If you check this option, then your customers will be limited to shipping options configured here. Otherwise, they'll be able to choose any existing shipping options even they are not configured here (zero shipping fee in this case).",
            ["Plugins.Shipping.FixedByWeightByTotal.Fields.LowerWeightLimit"] = "Lower weight limit",
            ["Plugins.Shipping.FixedByWeightByTotal.Fields.LowerWeightLimit.Hint"] = "Lower weight limit. This field can be used for \"per extra weight unit\" scenarios.",
            ["Plugins.Shipping.FixedByWeightByTotal.Fields.OrderSubtotalFrom"] = "Order subtotal from",
            ["Plugins.Shipping.FixedByWeightByTotal.Fields.OrderSubtotalFrom.Hint"] = "Order subtotal from.",
            ["Plugins.Shipping.FixedByWeightByTotal.Fields.OrderSubtotalTo"] = "Order subtotal to",
            ["Plugins.Shipping.FixedByWeightByTotal.Fields.OrderSubtotalTo.Hint"] = "Order subtotal to.",
            ["Plugins.Shipping.FixedByWeightByTotal.Fields.PercentageRateOfSubtotal"] = "Charge percentage (of subtotal)",
            ["Plugins.Shipping.FixedByWeightByTotal.Fields.PercentageRateOfSubtotal.Hint"] = "Charge percentage (of subtotal).",
            ["Plugins.Shipping.FixedByWeightByTotal.Fields.Rate"] = "Rate",
            ["Plugins.Shipping.FixedByWeightByTotal.Fields.RatePerWeightUnit"] = "Rate per weight unit",
            ["Plugins.Shipping.FixedByWeightByTotal.Fields.RatePerWeightUnit.Hint"] = "Rate per weight unit.",
            ["Plugins.Shipping.FixedByWeightByTotal.Fields.ShippingMethod"] = "Shipping method",
            ["Plugins.Shipping.FixedByWeightByTotal.Fields.ShippingMethod.Hint"] = "Choose shipping method.",
            ["Plugins.Shipping.FixedByWeightByTotal.Fields.StateProvince"] = "State / province",
            ["Plugins.Shipping.FixedByWeightByTotal.Fields.StateProvince.Hint"] = "If an asterisk is selected, then this shipping rate will apply to all customers from the given country, regardless of the state.",
            ["Plugins.Shipping.FixedByWeightByTotal.Fields.Store"] = "Store",
            ["Plugins.Shipping.FixedByWeightByTotal.Fields.Store.Hint"] = "If an asterisk is selected, then this shipping rate will apply to all stores.",
            ["Plugins.Shipping.FixedByWeightByTotal.Fields.TransitDays"] = "Transit days",
            ["Plugins.Shipping.FixedByWeightByTotal.Fields.TransitDays.Hint"] = "The number of days of delivery of the goods.",
            ["Plugins.Shipping.FixedByWeightByTotal.Fields.Warehouse"] = "Warehouse",
            ["Plugins.Shipping.FixedByWeightByTotal.Fields.Warehouse.Hint"] = "If an asterisk is selected, then this shipping rate will apply to all warehouses.",
            ["Plugins.Shipping.FixedByWeightByTotal.Fields.WeightFrom"] = "Order weight from",
            ["Plugins.Shipping.FixedByWeightByTotal.Fields.WeightFrom.Hint"] = "Order weight from.",
            ["Plugins.Shipping.FixedByWeightByTotal.Fields.WeightTo"] = "Order weight to",
            ["Plugins.Shipping.FixedByWeightByTotal.Fields.WeightTo.Hint"] = "Order weight to.",
            ["Plugins.Shipping.FixedByWeightByTotal.Fields.Zip"] = "Zip",
            ["Plugins.Shipping.FixedByWeightByTotal.Fields.Zip.Hint"] = "Zip / postal code. If zip is empty, then this shipping rate will apply to all customers from the given country or state, regardless of the zip code.",
            ["Plugins.Shipping.FixedByWeightByTotal.Fixed"] = "Fixed Rate",
            ["Plugins.Shipping.FixedByWeightByTotal.Formula"] = "Formula to calculate rates",
            ["Plugins.Shipping.FixedByWeightByTotal.Formula.Value"] = "[additional fixed cost] + ([order total weight] - [lower weight limit]) * [rate per weight unit] + [order subtotal] * [charge percentage]",
            ["Plugins.Shipping.FixedByWeightByTotal.ShippingByWeight"] = "By Weight"
        });

        await base.InstallAsync();
    }

    /// <summary>
    /// Uninstall plugin
    /// </summary>
    /// <returns>A task that represents the asynchronous operation</returns>
    public override async Task UninstallAsync()
    {
        //settings
        await _settingService.DeleteSettingAsync<FixedByWeightByTotalSettings>();

        //fixed rates
        var fixedRates = await (await _shippingService.GetAllShippingMethodsAsync())
            .SelectAwait(async shippingMethod => await _settingService.GetSettingAsync(
                string.Format(FixedByWeightByTotalDefaults.FIXED_RATE_SETTINGS_KEY, shippingMethod.Id)))
            .Where(setting => setting != null).ToListAsync();
        await _settingService.DeleteSettingsAsync(fixedRates);

        //locales
        await _localizationService.DeleteLocaleResourcesAsync("Plugins.Shipping.FixedByWeightByTotal");

        await base.UninstallAsync();
    }



    #region Widget plugin

    public bool HideInWidgetList => false;

    public Type GetWidgetViewComponent(string widgetZone)
    {
        if (string.Equals(widgetZone, AdminWidgetZones.ProductAttributeValueDetailsBottom, StringComparison.OrdinalIgnoreCase)
            || string.Equals(widgetZone, AdminWidgetZones.ProductAttributeValueDetailsTop, StringComparison.OrdinalIgnoreCase))
            return typeof(HoodAttributeValueShippingDimensionViewComponent);

        if (string.Equals(widgetZone, AdminWidgetZones.OrderShipmentDetailsButtons, StringComparison.OrdinalIgnoreCase))
            return typeof(HoodNavlungoShipmentLinksViewComponent);

        if (IsAdminProductEditZone(widgetZone))
            return typeof(HoodProductProductionTimeAdminViewComponent);

        if (IsPublicProductDetailsZone(widgetZone))
            return typeof(HoodProductProductionTimeViewComponent);

        return typeof(HoodNavlungoShipmentLinksViewComponent);
    }

    public Task<IList<string>> GetWidgetZonesAsync()
    {
        return Task.FromResult<IList<string>>(new List<string>
        {
            AdminWidgetZones.OrderShipmentDetailsButtons,
            AdminWidgetZones.ProductAttributeValueDetailsBottom,

            // Admin product edit page.
            AdminWidgetZones.ProductDetailsBlock,

            // Public product details page. For Element theme this must stay inside the .overview column.
            // EssentialBottom / BeforeCollateral are outside the overview float and can break the gallery/overview layout.
            PublicWidgetZones.ProductDetailsOverviewBottom
        });
    }

    private static bool IsAdminProductEditZone(string widgetZone)
    {
        if (string.IsNullOrWhiteSpace(widgetZone))
            return false;

        return string.Equals(widgetZone, AdminWidgetZones.ProductDetailsBlock, StringComparison.OrdinalIgnoreCase)
            || widgetZone.Contains("admin_product_details", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPublicProductDetailsZone(string widgetZone)
    {
        if (string.IsNullOrWhiteSpace(widgetZone))
            return false;

        return string.Equals(widgetZone, PublicWidgetZones.ProductDetailsOverviewBottom, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #endregion
}