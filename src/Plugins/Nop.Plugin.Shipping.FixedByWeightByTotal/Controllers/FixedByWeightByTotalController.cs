using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Directory;
using Nop.Plugin.Shipping.FixedByWeightByTotal.Domain;
using Nop.Plugin.Shipping.FixedByWeightByTotal.Models;
using Nop.Plugin.Shipping.FixedByWeightByTotal.Services;
using Nop.Plugin.Shipping.FixedByWeightByTotal.Services.ShippingDimensions;
using Nop.Plugin.Shipping.FixedByWeightByTotal.Services.ProductionTime;
using Nop.Plugin.Shipping.FixedByWeightByTotal.Models.ProductionTime;
using Nop.Plugin.Shipping.FixedByWeightByTotal.Domain;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Security;
using Nop.Services.Shipping;
using Nop.Services.Stores;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Models.Extensions;
using Nop.Web.Framework.Mvc;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Shipping.FixedByWeightByTotal.Controllers;

[AuthorizeAdmin]
[Area(AreaNames.ADMIN)]
[AutoValidateAntiforgeryToken]
public class FixedByWeightByTotalController : BasePluginController
{
    #region Fields

    protected readonly CurrencySettings _currencySettings;
    protected readonly FixedByWeightByTotalSettings _fixedByWeightByTotalSettings;
    protected readonly ICountryService _countryService;
    protected readonly ICurrencyService _currencyService;
    protected readonly ILocalizationService _localizationService;
    protected readonly IMeasureService _measureService;
    protected readonly IPermissionService _permissionService;
    protected readonly ISettingService _settingService;
    protected readonly IShippingByWeightByTotalService _shippingByWeightService;
    protected readonly IProductShippingDimensionService _productShippingDimensionService;
    protected readonly IProductProductionTimeService _productProductionTimeService;
    protected readonly IShippingService _shippingService;
    protected readonly IStateProvinceService _stateProvinceService;
    protected readonly IStoreService _storeService;
    protected readonly IGenericAttributeService _genericAttributeService;
    protected readonly IWorkContext _workContext;
    protected readonly MeasureSettings _measureSettings;

    #endregion

    #region Ctor

    public FixedByWeightByTotalController(CurrencySettings currencySettings,
        FixedByWeightByTotalSettings fixedByWeightByTotalSettings,
        ICountryService countryService,
        ICurrencyService currencyService,
        ILocalizationService localizationService,
        IMeasureService measureService,
        IPermissionService permissionService,
        ISettingService settingService,
        IShippingByWeightByTotalService shippingByWeightService,
        IProductShippingDimensionService productShippingDimensionService,
        IProductProductionTimeService productProductionTimeService,
        IShippingService shippingService,
        IStateProvinceService stateProvinceService,
        IStoreService storeService,
        IGenericAttributeService genericAttributeService,
        IWorkContext workContext,
        MeasureSettings measureSettings)
    {
        _currencySettings = currencySettings;
        _fixedByWeightByTotalSettings = fixedByWeightByTotalSettings;
        _countryService = countryService;
        _currencyService = currencyService;
        _localizationService = localizationService;
        _measureService = measureService;
        _permissionService = permissionService;
        _settingService = settingService;
        _shippingByWeightService = shippingByWeightService;
        _productShippingDimensionService = productShippingDimensionService;
        _productProductionTimeService = productProductionTimeService;
        _stateProvinceService = stateProvinceService;
        _shippingService = shippingService;
        _storeService = storeService;
        _genericAttributeService = genericAttributeService;
        _workContext = workContext;
        _measureSettings = measureSettings;
    }

    #endregion

    #region Methods

    [CheckPermission(StandardPermission.Configuration.MANAGE_SHIPPING_SETTINGS)]
    public async Task<IActionResult> Configure(bool showtour = false)
    {
        var model = new ConfigurationModel
        {
            LimitMethodsToCreated = _fixedByWeightByTotalSettings.LimitMethodsToCreated,
            ShippingByWeightByTotalEnabled = _fixedByWeightByTotalSettings.ShippingByWeightByTotalEnabled,
            HoodNavlungoChargeableWeightEnabled = _fixedByWeightByTotalSettings.HoodNavlungoChargeableWeightEnabled,
            HoodNavlungoDimensionalWeightDivisor = _fixedByWeightByTotalSettings.HoodNavlungoDimensionalWeightDivisor <= 0 ? 5000m : _fixedByWeightByTotalSettings.HoodNavlungoDimensionalWeightDivisor,
            HoodNavlungoRateWeightMultiplier = _fixedByWeightByTotalSettings.HoodNavlungoRateWeightMultiplier <= 0 ? 1000m : _fixedByWeightByTotalSettings.HoodNavlungoRateWeightMultiplier,
            HoodPttPostServiceEnabled = _fixedByWeightByTotalSettings.HoodPttPostServiceEnabled,
            HoodPttEligibleProductIdsCsv = _fixedByWeightByTotalSettings.HoodPttEligibleProductIdsCsv,
            HoodPttEligibleCategoryIdsCsv = _fixedByWeightByTotalSettings.HoodPttEligibleCategoryIdsCsv,
            HoodPttMethodName = string.IsNullOrWhiteSpace(_fixedByWeightByTotalSettings.HoodPttMethodName) ? "Post service" : _fixedByWeightByTotalSettings.HoodPttMethodName,
            HoodPttDeliveryKind = string.IsNullOrWhiteSpace(_fixedByWeightByTotalSettings.HoodPttDeliveryKind) ? "YD KOLİ" : _fixedByWeightByTotalSettings.HoodPttDeliveryKind,
            HoodPttDistributionType = string.IsNullOrWhiteSpace(_fixedByWeightByTotalSettings.HoodPttDistributionType) ? "UC" : _fixedByWeightByTotalSettings.HoodPttDistributionType,
            HoodPttAdditionalService = _fixedByWeightByTotalSettings.HoodPttAdditionalService,
            HoodPttMaxSingleDimensionCm = _fixedByWeightByTotalSettings.HoodPttMaxSingleDimensionCm <= 0 ? 150m : _fixedByWeightByTotalSettings.HoodPttMaxSingleDimensionCm,
            HoodPttMaxGirthCm = _fixedByWeightByTotalSettings.HoodPttMaxGirthCm < 0 ? 0 : _fixedByWeightByTotalSettings.HoodPttMaxGirthCm,
            HoodPttTransitMinDays = _fixedByWeightByTotalSettings.HoodPttTransitMinDays < 0 ? 0 : _fixedByWeightByTotalSettings.HoodPttTransitMinDays,
            HoodPttTransitMaxDays = _fixedByWeightByTotalSettings.HoodPttTransitMaxDays <= 0 ? 20 : _fixedByWeightByTotalSettings.HoodPttTransitMaxDays,
            HoodPttLivePriceMultiplier = _fixedByWeightByTotalSettings.HoodPttLivePriceMultiplier <= 0 ? 1m : _fixedByWeightByTotalSettings.HoodPttLivePriceMultiplier,
            HoodPttAdditionalFixedMarkup = _fixedByWeightByTotalSettings.HoodPttAdditionalFixedMarkup,
            HoodPttRequestTimeoutSeconds = _fixedByWeightByTotalSettings.HoodPttRequestTimeoutSeconds <= 0 ? 8 : _fixedByWeightByTotalSettings.HoodPttRequestTimeoutSeconds
        };

        //stores
        model.AvailableStores.Add(new SelectListItem { Text = "*", Value = "0" });
        foreach (var store in await _storeService.GetAllStoresAsync())
            model.AvailableStores.Add(new SelectListItem { Text = store.Name, Value = store.Id.ToString() });
        //warehouses
        model.AvailableWarehouses.Add(new SelectListItem { Text = "*", Value = "0" });
        foreach (var warehouses in await _shippingService.GetAllWarehousesAsync())
            model.AvailableWarehouses.Add(new SelectListItem { Text = warehouses.Name, Value = warehouses.Id.ToString() });
        //shipping methods
        model.AvailableShippingMethods.Add(new SelectListItem { Text = "*", Value = "0" });
        foreach (var sm in await _shippingService.GetAllShippingMethodsAsync())
            model.AvailableShippingMethods.Add(new SelectListItem { Text = sm.Name, Value = sm.Id.ToString() });
        //countries
        model.AvailableCountries.Add(new SelectListItem { Text = "*", Value = "0" });
        var countries = await _countryService.GetAllCountriesAsync();
        foreach (var c in countries)
            model.AvailableCountries.Add(new SelectListItem { Text = c.Name, Value = c.Id.ToString() });
        //states
        model.AvailableStates.Add(new SelectListItem { Text = "*", Value = "0" });

        model.SetGridPageSize();

        //show configuration tour
        if (showtour)
        {
            var customer = await _workContext.GetCurrentCustomerAsync();
            var hideCard = await _genericAttributeService.GetAttributeAsync<bool>(customer, NopCustomerDefaults.HideConfigurationStepsAttribute);
            var closeCard = await _genericAttributeService.GetAttributeAsync<bool>(customer, NopCustomerDefaults.CloseConfigurationStepsAttribute);

            if (!hideCard && !closeCard)
                ViewBag.ShowTour = true;
        }

        return View("~/Plugins/Shipping.FixedByWeightByTotal/Views/Configure.cshtml", model);
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_SHIPPING_SETTINGS)]
    public async Task<IActionResult> Configure(ConfigurationModel model)
    {
        //save settings
        _fixedByWeightByTotalSettings.LimitMethodsToCreated = model.LimitMethodsToCreated;
        _fixedByWeightByTotalSettings.HoodNavlungoChargeableWeightEnabled = model.HoodNavlungoChargeableWeightEnabled;
        _fixedByWeightByTotalSettings.HoodNavlungoDimensionalWeightDivisor = model.HoodNavlungoDimensionalWeightDivisor <= 0 ? 5000m : model.HoodNavlungoDimensionalWeightDivisor;
        _fixedByWeightByTotalSettings.HoodNavlungoRateWeightMultiplier = model.HoodNavlungoRateWeightMultiplier <= 0 ? 1000m : model.HoodNavlungoRateWeightMultiplier;
        _fixedByWeightByTotalSettings.HoodPttPostServiceEnabled = model.HoodPttPostServiceEnabled;
        _fixedByWeightByTotalSettings.HoodPttEligibleProductIdsCsv = model.HoodPttEligibleProductIdsCsv;
        _fixedByWeightByTotalSettings.HoodPttEligibleCategoryIdsCsv = model.HoodPttEligibleCategoryIdsCsv;
        _fixedByWeightByTotalSettings.HoodPttMethodName = string.IsNullOrWhiteSpace(model.HoodPttMethodName) ? "Post service" : model.HoodPttMethodName.Trim();
        _fixedByWeightByTotalSettings.HoodPttDeliveryKind = string.IsNullOrWhiteSpace(model.HoodPttDeliveryKind) ? "YD KOLİ" : model.HoodPttDeliveryKind.Trim();
        _fixedByWeightByTotalSettings.HoodPttDistributionType = string.IsNullOrWhiteSpace(model.HoodPttDistributionType) ? "UC" : model.HoodPttDistributionType.Trim();
        _fixedByWeightByTotalSettings.HoodPttAdditionalService = model.HoodPttAdditionalService?.Trim();
        _fixedByWeightByTotalSettings.HoodPttMaxSingleDimensionCm = model.HoodPttMaxSingleDimensionCm <= 0 ? 150m : model.HoodPttMaxSingleDimensionCm;
        _fixedByWeightByTotalSettings.HoodPttMaxGirthCm = model.HoodPttMaxGirthCm < 0 ? 0 : model.HoodPttMaxGirthCm;
        _fixedByWeightByTotalSettings.HoodPttTransitMinDays = Math.Max(0, model.HoodPttTransitMinDays);
        _fixedByWeightByTotalSettings.HoodPttTransitMaxDays = Math.Max(Math.Max(0, model.HoodPttTransitMinDays), model.HoodPttTransitMaxDays);
        _fixedByWeightByTotalSettings.HoodPttLivePriceMultiplier = model.HoodPttLivePriceMultiplier <= 0 ? 1m : model.HoodPttLivePriceMultiplier;
        _fixedByWeightByTotalSettings.HoodPttAdditionalFixedMarkup = model.HoodPttAdditionalFixedMarkup;
        _fixedByWeightByTotalSettings.HoodPttRequestTimeoutSeconds = model.HoodPttRequestTimeoutSeconds <= 0 ? 8 : model.HoodPttRequestTimeoutSeconds;
        await _settingService.SaveSettingAsync(_fixedByWeightByTotalSettings);

        return Json(new { Result = true });
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_SHIPPING_SETTINGS)]
    public async Task<IActionResult> SaveMode(bool value)
    {
        //save settings
        _fixedByWeightByTotalSettings.ShippingByWeightByTotalEnabled = value;
        await _settingService.SaveSettingAsync(_fixedByWeightByTotalSettings);

        return Json(new { Result = true });
    }

    #region Fixed rate

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_SHIPPING_SETTINGS)]
    public async Task<IActionResult> FixedShippingRateList(ConfigurationModel searchModel)
    {
        var shippingMethods = (await _shippingService.GetAllShippingMethodsAsync()).ToPagedList(searchModel);

        var gridModel = await new FixedRateListModel().PrepareToGridAsync(searchModel, shippingMethods, () =>
        {
            return shippingMethods.SelectAwait(async shippingMethod => new FixedRateModel
            {
                ShippingMethodId = shippingMethod.Id,
                ShippingMethodName = shippingMethod.Name,

                Rate = await _settingService
                    .GetSettingByKeyAsync<decimal>(string.Format(FixedByWeightByTotalDefaults.FIXED_RATE_SETTINGS_KEY, shippingMethod.Id)),
                TransitDays = await _settingService
                    .GetSettingByKeyAsync<int?>(string.Format(FixedByWeightByTotalDefaults.TRANSIT_DAYS_SETTINGS_KEY, shippingMethod.Id))
            });
        });

        return Json(gridModel);
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_SHIPPING_SETTINGS)]
    public async Task<IActionResult> UpdateFixedShippingRate(FixedRateModel model)
    {
        await _settingService.SetSettingAsync(string.Format(FixedByWeightByTotalDefaults.FIXED_RATE_SETTINGS_KEY, model.ShippingMethodId), model.Rate, 0, false);
        await _settingService.SetSettingAsync(string.Format(FixedByWeightByTotalDefaults.TRANSIT_DAYS_SETTINGS_KEY, model.ShippingMethodId), model.TransitDays, 0, false);

        await _settingService.ClearCacheAsync();

        return new NullJsonResult();
    }

    #endregion

    #region Rate by weight

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_SHIPPING_SETTINGS)]
    public async Task<IActionResult> RateByWeightByTotalList(ConfigurationModel searchModel, ConfigurationModel filter)
    {
        //var records = _shippingByWeightService.GetAll(command.Page - 1, command.PageSize);
        var records = await _shippingByWeightService.FindRecordsAsync(
            pageIndex: searchModel.Page - 1,
            pageSize: searchModel.PageSize,
            storeId: filter.SearchStoreId,
            warehouseId: filter.SearchWarehouseId,
            countryId: filter.SearchCountryId,
            stateProvinceId: filter.SearchStateProvinceId,
            zip: filter.SearchZip,
            shippingMethodId: filter.SearchShippingMethodId,
            weight: null,
            orderSubtotal: null
        );

        var gridModel = await new ShippingByWeightByTotalListModel().PrepareToGridAsync(searchModel, records, () =>
        {
            return records.SelectAwait(async record =>
            {
                var model = new ShippingByWeightByTotalModel
                {
                    Id = record.Id,
                    StoreId = record.StoreId,
                    StoreName = (await _storeService.GetStoreByIdAsync(record.StoreId))?.Name ?? "*",
                    WarehouseId = record.WarehouseId,
                    WarehouseName = (await _shippingService.GetWarehouseByIdAsync(record.WarehouseId))?.Name ?? "*",
                    ShippingMethodId = record.ShippingMethodId,
                    ShippingMethodName = (await _shippingService.GetShippingMethodByIdAsync(record.ShippingMethodId))?.Name ?? "Unavailable",
                    CountryId = record.CountryId,
                    CountryName = (await _countryService.GetCountryByIdAsync(record.CountryId))?.Name ?? "*",
                    StateProvinceId = record.StateProvinceId,
                    StateProvinceName = (await _stateProvinceService.GetStateProvinceByIdAsync(record.StateProvinceId))?.Name ?? "*",
                    WeightFrom = record.WeightFrom,
                    WeightTo = record.WeightTo,
                    OrderSubtotalFrom = record.OrderSubtotalFrom,
                    OrderSubtotalTo = record.OrderSubtotalTo,
                    AdditionalFixedCost = record.AdditionalFixedCost,
                    PercentageRateOfSubtotal = record.PercentageRateOfSubtotal,
                    RatePerWeightUnit = record.RatePerWeightUnit,
                    LowerWeightLimit = record.LowerWeightLimit,
                    Zip = !string.IsNullOrEmpty(record.Zip) ? record.Zip : "*"
                };

                var htmlSb = new StringBuilder("<div>");
                htmlSb.AppendFormat("{0}: {1}",
                    await _localizationService.GetResourceAsync("Plugins.Shipping.FixedByWeightByTotal.Fields.WeightFrom"),
                    model.WeightFrom);
                htmlSb.Append("<br />");
                htmlSb.AppendFormat("{0}: {1}",
                    await _localizationService.GetResourceAsync("Plugins.Shipping.FixedByWeightByTotal.Fields.WeightTo"),
                    model.WeightTo);
                htmlSb.Append("<br />");
                htmlSb.AppendFormat("{0}: {1}",
                    await _localizationService.GetResourceAsync("Plugins.Shipping.FixedByWeightByTotal.Fields.OrderSubtotalFrom"),
                    model.OrderSubtotalFrom);
                htmlSb.Append("<br />");
                htmlSb.AppendFormat("{0}: {1}",
                    await _localizationService.GetResourceAsync("Plugins.Shipping.FixedByWeightByTotal.Fields.OrderSubtotalTo"),
                    model.OrderSubtotalTo);
                htmlSb.Append("<br />");
                htmlSb.AppendFormat("{0}: {1}",
                    await _localizationService.GetResourceAsync("Plugins.Shipping.FixedByWeightByTotal.Fields.AdditionalFixedCost"),
                    model.AdditionalFixedCost);
                htmlSb.Append("<br />");
                htmlSb.AppendFormat("{0}: {1}",
                    await _localizationService.GetResourceAsync("Plugins.Shipping.FixedByWeightByTotal.Fields.RatePerWeightUnit"),
                    model.RatePerWeightUnit);
                htmlSb.Append("<br />");
                htmlSb.AppendFormat("{0}: {1}",
                    await _localizationService.GetResourceAsync("Plugins.Shipping.FixedByWeightByTotal.Fields.LowerWeightLimit"),
                    model.LowerWeightLimit);
                htmlSb.Append("<br />");
                htmlSb.AppendFormat("{0}: {1}",
                    await _localizationService.GetResourceAsync("Plugins.Shipping.FixedByWeightByTotal.Fields.PercentageRateOfSubtotal"),
                    model.PercentageRateOfSubtotal);

                htmlSb.Append("</div>");
                model.DataHtml = htmlSb.ToString();

                return model;
            });
        });

        return Json(gridModel);
    }

    [CheckPermission(StandardPermission.Configuration.MANAGE_SHIPPING_SETTINGS)]
    public async Task<IActionResult> AddRateByWeightByTotalPopup()
    {
        var model = new ShippingByWeightByTotalModel
        {
            PrimaryStoreCurrencyCode = (await _currencyService.GetCurrencyByIdAsync(_currencySettings.PrimaryStoreCurrencyId))?.CurrencyCode,
            BaseWeightIn = (await _measureService.GetMeasureWeightByIdAsync(_measureSettings.BaseWeightId))?.Name,
            WeightTo = 1000000,
            OrderSubtotalTo = 1000000
        };

        var shippingMethods = await _shippingService.GetAllShippingMethodsAsync();
        if (!shippingMethods.Any())
            return Content("No shipping methods can be loaded");

        //stores
        model.AvailableStores.Add(new SelectListItem { Text = "*", Value = "0" });
        foreach (var store in await _storeService.GetAllStoresAsync())
            model.AvailableStores.Add(new SelectListItem { Text = store.Name, Value = store.Id.ToString() });
        //warehouses
        model.AvailableWarehouses.Add(new SelectListItem { Text = "*", Value = "0" });
        foreach (var warehouses in await _shippingService.GetAllWarehousesAsync())
            model.AvailableWarehouses.Add(new SelectListItem { Text = warehouses.Name, Value = warehouses.Id.ToString() });
        //shipping methods
        foreach (var sm in shippingMethods)
            model.AvailableShippingMethods.Add(new SelectListItem { Text = sm.Name, Value = sm.Id.ToString() });
        //countries
        model.AvailableCountries.Add(new SelectListItem { Text = "*", Value = "0" });
        var countries = await _countryService.GetAllCountriesAsync(showHidden: true);
        foreach (var c in countries)
            model.AvailableCountries.Add(new SelectListItem { Text = c.Name, Value = c.Id.ToString() });
        //states
        model.AvailableStates.Add(new SelectListItem { Text = "*", Value = "0" });

        return View("~/Plugins/Shipping.FixedByWeightByTotal/Views/AddRateByWeightByTotalPopup.cshtml", model);
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_SHIPPING_SETTINGS)]
    public async Task<IActionResult> AddRateByWeightByTotalPopup(ShippingByWeightByTotalModel model)
    {
        await _shippingByWeightService.InsertShippingByWeightRecordAsync(new ShippingByWeightByTotalRecord
        {
            StoreId = model.StoreId,
            WarehouseId = model.WarehouseId,
            CountryId = model.CountryId,
            StateProvinceId = model.StateProvinceId,
            Zip = model.Zip == "*" ? null : model.Zip,
            ShippingMethodId = model.ShippingMethodId,
            WeightFrom = model.WeightFrom,
            WeightTo = model.WeightTo,
            OrderSubtotalFrom = model.OrderSubtotalFrom,
            OrderSubtotalTo = model.OrderSubtotalTo,
            AdditionalFixedCost = model.AdditionalFixedCost,
            RatePerWeightUnit = model.RatePerWeightUnit,
            PercentageRateOfSubtotal = model.PercentageRateOfSubtotal,
            LowerWeightLimit = model.LowerWeightLimit,
            TransitDays = model.TransitDays
        });

        ViewBag.RefreshPage = true;

        return View("~/Plugins/Shipping.FixedByWeightByTotal/Views/AddRateByWeightByTotalPopup.cshtml", model);
    }

    [CheckPermission(StandardPermission.Configuration.MANAGE_SHIPPING_SETTINGS)]
    public async Task<IActionResult> EditRateByWeightByTotalPopup(int id)
    {
        var sbw = await _shippingByWeightService.GetByIdAsync(id);
        if (sbw == null)
            //no record found with the specified id
            return RedirectToAction("Configure");

        var model = new ShippingByWeightByTotalModel
        {
            Id = sbw.Id,
            StoreId = sbw.StoreId,
            WarehouseId = sbw.WarehouseId,
            CountryId = sbw.CountryId,
            StateProvinceId = sbw.StateProvinceId,
            Zip = sbw.Zip,
            ShippingMethodId = sbw.ShippingMethodId,
            WeightFrom = sbw.WeightFrom,
            WeightTo = sbw.WeightTo,
            OrderSubtotalFrom = sbw.OrderSubtotalFrom,
            OrderSubtotalTo = sbw.OrderSubtotalTo,
            AdditionalFixedCost = sbw.AdditionalFixedCost,
            PercentageRateOfSubtotal = sbw.PercentageRateOfSubtotal,
            RatePerWeightUnit = sbw.RatePerWeightUnit,
            LowerWeightLimit = sbw.LowerWeightLimit,
            PrimaryStoreCurrencyCode = (await _currencyService.GetCurrencyByIdAsync(_currencySettings.PrimaryStoreCurrencyId))?.CurrencyCode,
            BaseWeightIn = (await _measureService.GetMeasureWeightByIdAsync(_measureSettings.BaseWeightId))?.Name,
            TransitDays = sbw.TransitDays
        };

        var shippingMethods = await _shippingService.GetAllShippingMethodsAsync();
        if (!shippingMethods.Any())
            return Content("No shipping methods can be loaded");

        var selectedStore = await _storeService.GetStoreByIdAsync(sbw.StoreId);
        var selectedWarehouse = await _shippingService.GetWarehouseByIdAsync(sbw.WarehouseId);
        var selectedShippingMethod = await _shippingService.GetShippingMethodByIdAsync(sbw.ShippingMethodId);
        var selectedCountry = await _countryService.GetCountryByIdAsync(sbw.CountryId);
        var selectedState = await _stateProvinceService.GetStateProvinceByIdAsync(sbw.StateProvinceId);
        //stores
        model.AvailableStores.Add(new SelectListItem { Text = "*", Value = "0" });
        foreach (var store in await _storeService.GetAllStoresAsync())
            model.AvailableStores.Add(new SelectListItem { Text = store.Name, Value = store.Id.ToString(), Selected = (selectedStore != null && store.Id == selectedStore.Id) });
        //warehouses
        model.AvailableWarehouses.Add(new SelectListItem { Text = "*", Value = "0" });
        foreach (var warehouse in await _shippingService.GetAllWarehousesAsync())
            model.AvailableWarehouses.Add(new SelectListItem { Text = warehouse.Name, Value = warehouse.Id.ToString(), Selected = (selectedWarehouse != null && warehouse.Id == selectedWarehouse.Id) });
        //shipping methods
        foreach (var sm in shippingMethods)
            model.AvailableShippingMethods.Add(new SelectListItem { Text = sm.Name, Value = sm.Id.ToString(), Selected = (selectedShippingMethod != null && sm.Id == selectedShippingMethod.Id) });
        //countries
        model.AvailableCountries.Add(new SelectListItem { Text = "*", Value = "0" });
        var countries = await _countryService.GetAllCountriesAsync(showHidden: true);
        foreach (var c in countries)
            model.AvailableCountries.Add(new SelectListItem { Text = c.Name, Value = c.Id.ToString(), Selected = (selectedCountry != null && c.Id == selectedCountry.Id) });
        //states
        var states = selectedCountry != null ? (await _stateProvinceService.GetStateProvincesByCountryIdAsync(selectedCountry.Id, showHidden: true)).ToList() : new List<StateProvince>();
        model.AvailableStates.Add(new SelectListItem { Text = "*", Value = "0" });
        foreach (var s in states)
            model.AvailableStates.Add(new SelectListItem { Text = s.Name, Value = s.Id.ToString(), Selected = (selectedState != null && s.Id == selectedState.Id) });

        return View("~/Plugins/Shipping.FixedByWeightByTotal/Views/EditRateByWeightByTotalPopup.cshtml", model);
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_SHIPPING_SETTINGS)]
    public async Task<IActionResult> EditRateByWeightByTotalPopup(ShippingByWeightByTotalModel model)
    {
        var sbw = await _shippingByWeightService.GetByIdAsync(model.Id);
        if (sbw == null)
            //no record found with the specified id
            return RedirectToAction("Configure");

        sbw.StoreId = model.StoreId;
        sbw.WarehouseId = model.WarehouseId;
        sbw.CountryId = model.CountryId;
        sbw.StateProvinceId = model.StateProvinceId;
        sbw.Zip = model.Zip == "*" ? null : model.Zip;
        sbw.ShippingMethodId = model.ShippingMethodId;
        sbw.WeightFrom = model.WeightFrom;
        sbw.WeightTo = model.WeightTo;
        sbw.OrderSubtotalFrom = model.OrderSubtotalFrom;
        sbw.OrderSubtotalTo = model.OrderSubtotalTo;
        sbw.AdditionalFixedCost = model.AdditionalFixedCost;
        sbw.RatePerWeightUnit = model.RatePerWeightUnit;
        sbw.PercentageRateOfSubtotal = model.PercentageRateOfSubtotal;
        sbw.LowerWeightLimit = model.LowerWeightLimit;
        sbw.TransitDays = model.TransitDays;

        await _shippingByWeightService.UpdateShippingByWeightRecordAsync(sbw);

        ViewBag.RefreshPage = true;

        return View("~/Plugins/Shipping.FixedByWeightByTotal/Views/EditRateByWeightByTotalPopup.cshtml", model);
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_SHIPPING_SETTINGS)]
    public async Task<IActionResult> DeleteRateByWeightByTotal(int id)
    {
        var sbw = await _shippingByWeightService.GetByIdAsync(id);
        if (sbw != null)
            await _shippingByWeightService.DeleteShippingByWeightRecordAsync(sbw);

        return new NullJsonResult();
    }

    #endregion



    #region Hood/Navlungo dimension rules and learning

    [CheckPermission(StandardPermission.Configuration.MANAGE_SHIPPING_SETTINGS)]
    public async Task<IActionResult> ShippingDimensionRules(int productId = 0)
    {
        var rules = productId > 0
            ? await _productShippingDimensionService.GetRulesByProductIdAsync(productId, activeOnly: false)
            : new List<HoodProductShippingDimensionRule>();

        ViewBag.ProductId = productId;
        ViewBag.DriverAttributeOptions = productId > 0
            ? await _productShippingDimensionService.GetProductAttributeDriverOptionsAsync(productId)
            : new List<Nop.Plugin.Shipping.FixedByWeightByTotal.Models.ShippingDimensions.ShippingDriverAttributeModel>();
        return View("~/Plugins/Shipping.FixedByWeightByTotal/Views/ShippingDimensions/Rules.cshtml", rules);
    }


    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_SHIPPING_SETTINGS)]
    public async Task<IActionResult> SaveShippingDriverAttribute(int productId, int productAttributeMappingId, string ruleType = "ATTRIBUTE_VALUE", bool isActive = true)
    {
        await _productShippingDimensionService.SaveShippingDriverAttributeAsync(productId, productAttributeMappingId, ruleType, isActive);
        return Json(new { Result = true });
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_SHIPPING_SETTINGS)]
    public async Task<IActionResult> DeleteShippingDriverAttribute(int id)
    {
        await _productShippingDimensionService.DeleteShippingDriverAttributeAsync(id);
        return Json(new { Result = true });
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_SHIPPING_SETTINGS)]
    public async Task<IActionResult> SaveShippingDimensionRule(int id, int productId, int? productAttributeValueId,
        string attributeValueIdsCsv, string ruleType, decimal lengthCm, decimal widthCm, decimal heightCm,
        decimal? weightGram, decimal divisor, int packageCount, bool isShipSeparately, bool isActive)
    {
        var rule = id > 0 ? await _productShippingDimensionService.GetRuleByIdAsync(id) : null;
        rule ??= new HoodProductShippingDimensionRule { CreatedOnUtc = DateTime.UtcNow };

        rule.ProductId = productId;
        rule.ProductAttributeValueId = productAttributeValueId;
        rule.AttributeValueIdsCsv = attributeValueIdsCsv;
        rule.RuleType = string.IsNullOrWhiteSpace(ruleType) ? "PRODUCT_DEFAULT" : ruleType;
        rule.LengthCm = lengthCm;
        rule.WidthCm = widthCm;
        rule.HeightCm = heightCm;
        rule.WeightGram = weightGram;
        rule.Divisor = divisor <= 0 ? 5000m : divisor;
        rule.PackageCount = packageCount <= 0 ? 1 : packageCount;
        rule.IsShipSeparately = isShipSeparately;
        rule.IsActive = isActive;
        rule.Source = "Admin manual";

        if (rule.Id > 0)
            await _productShippingDimensionService.UpdateRuleAsync(rule);
        else
            await _productShippingDimensionService.InsertRuleAsync(rule);

        return Json(new { Result = true, rule.Id });
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_SHIPPING_SETTINGS)]
    public async Task<IActionResult> DeleteShippingDimensionRule(int id)
    {
        var rule = await _productShippingDimensionService.GetRuleByIdAsync(id);
        if (rule != null)
            await _productShippingDimensionService.DeleteRuleAsync(rule);

        return Json(new { Result = true });
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_SHIPPING_SETTINGS)]
    public async Task<IActionResult> SaveAttributeValueShippingDimension(int productId, int productAttributeValueId, bool enabled, string ruleType,
        decimal? lengthCm, decimal? widthCm, decimal? heightCm, decimal? weightGram, decimal divisor = 5000, int packageCount = 1, bool isShipSeparately = false)
    {
        await _productShippingDimensionService.SaveAttributeValueDimensionAsync(productId, productAttributeValueId, enabled, ruleType,
            lengthCm, widthCm, heightCm, weightGram, divisor, packageCount, isShipSeparately);

        return Json(new { Result = true });
    }



    [CheckPermission(StandardPermission.Configuration.MANAGE_SHIPPING_SETTINGS)]
    public async Task<IActionResult> ShippingDimensionExclusions()
    {
        var exclusions = await _productShippingDimensionService.GetExclusionsAsync(activeOnly: false);
        return View("~/Plugins/Shipping.FixedByWeightByTotal/Views/ShippingDimensions/Exclusions.cshtml", exclusions);
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_SHIPPING_SETTINGS)]
    public async Task<IActionResult> SaveShippingDimensionExclusion(int productId, string reason, bool isActive = true)
    {
        await _productShippingDimensionService.SaveExclusionAsync(productId, reason, isActive);
        return Json(new { Result = true });
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_SHIPPING_SETTINGS)]
    public async Task<IActionResult> DeleteShippingDimensionExclusion(int id)
    {
        await _productShippingDimensionService.DeleteExclusionAsync(id);
        return Json(new { Result = true });
    }

    [CheckPermission(StandardPermission.Configuration.MANAGE_SHIPPING_SETTINGS)]
    public IActionResult CategoryBulkApplyDimensions(int categoryId = 0, int sourceProductId = 0)
    {
        ViewBag.CategoryId = categoryId;
        ViewBag.SourceProductId = sourceProductId;
        return View("~/Plugins/Shipping.FixedByWeightByTotal/Views/ShippingDimensions/BulkApply.cshtml");
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_SHIPPING_SETTINGS)]
    public async Task<IActionResult> CategoryBulkApplyDimensionsPost(int categoryId, int sourceProductId, bool includeSubcategories = true, bool overwriteExisting = false)
    {
        var result = await _productShippingDimensionService.BulkApplyRulesToCategoryAsync(categoryId, sourceProductId, includeSubcategories, overwriteExisting);
        return Json(result);
    }

    [CheckPermission(StandardPermission.Configuration.MANAGE_SHIPPING_SETTINGS)]
    public async Task<IActionResult> NavlungoShipmentLinks(int? shipmentId = null, int? orderId = null, string trackingNumber = null)
    {
        var links = await _productShippingDimensionService.GetShipmentLinksAsync(shipmentId, orderId, trackingNumber);
        ViewBag.ShipmentId = shipmentId;
        ViewBag.OrderId = orderId;
        ViewBag.TrackingNumber = trackingNumber;
        return View("~/Plugins/Shipping.FixedByWeightByTotal/Views/ShippingDimensions/ShipmentLinks.cshtml", links);
    }

    [CheckPermission(StandardPermission.Configuration.MANAGE_SHIPPING_SETTINGS)]
    public async Task<IActionResult> NavlungoLearning(int minimumSampleCount = 1, bool groupByProduct = false)
    {
        var suggestions = await _productShippingDimensionService.GetLearningSuggestionsAsync(minimumSampleCount);
        ViewBag.MinimumSampleCount = minimumSampleCount;
        ViewBag.GroupByProduct = groupByProduct;
        return View("~/Plugins/Shipping.FixedByWeightByTotal/Views/ShippingDimensions/Learning.cshtml", suggestions);
    }

    [CheckPermission(StandardPermission.Configuration.MANAGE_SHIPPING_SETTINGS)]
    public async Task<IActionResult> NavlungoLearningDetails(int productId, string attributeHash = null, int? productAttributeValueId = null, string ruleType = null)
    {
        var details = await _productShippingDimensionService.GetLearningSuggestionDetailsAsync(productId, attributeHash, productAttributeValueId, ruleType);

        ViewBag.ProductId = productId;
        ViewBag.AttributeHash = attributeHash;
        ViewBag.ProductAttributeValueId = productAttributeValueId;
        ViewBag.RuleType = ruleType;

        return View("~/Plugins/Shipping.FixedByWeightByTotal/Views/ShippingDimensions/LearningDetails.cshtml", details);
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_SHIPPING_SETTINGS)]
    public async Task<IActionResult> ApplyNavlungoLearning(int minimumSampleCount = 2, bool overwriteExisting = false, string ruleTypeFilter = null)
    {
        var applied = await _productShippingDimensionService.ApplyLearningSuggestionsAsync(minimumSampleCount, overwriteExisting, ruleTypeFilter);
        return Json(new { Result = true, Applied = applied, RuleTypeFilter = ruleTypeFilter });
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_SHIPPING_SETTINGS)]
    public async Task<IActionResult> ApplyNavlungoProductProfile(int productId, int minimumSampleCount = 1, bool overwriteExisting = false, string profileType = null)
    {
        var applied = await _productShippingDimensionService.ApplyLearningProductProfileAsync(productId, minimumSampleCount, overwriteExisting, profileType);
        return Json(new { Result = true, Applied = applied, ProductId = productId, ProfileType = profileType });
    }



    #region Hood production time

    [CheckPermission(StandardPermission.Configuration.MANAGE_SHIPPING_SETTINGS)]
    public async Task<IActionResult> ProductProductionTimes(int productId = 0, bool onlyEnabled = false)
    {
        var model = await _productProductionTimeService.SearchAsync(productId, onlyEnabled, 300);
        ViewBag.ProductId = productId;
        ViewBag.OnlyEnabled = onlyEnabled;
        return View("~/Plugins/Shipping.FixedByWeightByTotal/Views/ProductionTime/Manage.cshtml", model);
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_SHIPPING_SETTINGS)]
    public async Task<IActionResult> SaveProductProductionTime(ProductProductionTimeModel model)
    {
        if (model == null || model.ProductId <= 0)
            return Json(new { Result = false, Error = "ProductId is required" });

        await _productProductionTimeService.SaveAsync(model, "Admin manual");
        return Json(new { Result = true, ProductId = model.ProductId });
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_SHIPPING_SETTINGS)]
    public async Task<IActionResult> ExtractProductProductionTime(int productId, bool save = false)
    {
        var model = await _productProductionTimeService.ExtractFromDescriptionAsync(productId, save);
        return Json(new { Result = true, Model = model });
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_SHIPPING_SETTINGS)]
    public async Task<IActionResult> BulkExtractProductProductionTimes(bool onlyMissing = true, int maxProducts = 1000, bool publishedOnly = true)
    {
        var result = await _productProductionTimeService.BulkExtractFromDescriptionsAsync(onlyMissing, maxProducts, publishedOnly);
        return Json(new
        {
            Result = true,
            result.Scanned,
            result.Saved,
            result.Skipped,
            result.Errors
        });
    }

    #endregion

    #endregion

    #endregion
}