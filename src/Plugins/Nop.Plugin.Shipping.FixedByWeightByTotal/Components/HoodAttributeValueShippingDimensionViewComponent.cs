using Microsoft.AspNetCore.Mvc;
using Nop.Core.Domain.Catalog;
using Nop.Data;
using Nop.Plugin.Shipping.FixedByWeightByTotal.Models.ShippingDimensions;
using Nop.Plugin.Shipping.FixedByWeightByTotal.Services.ShippingDimensions;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Shipping.FixedByWeightByTotal.Components;

public class HoodAttributeValueShippingDimensionViewComponent : NopViewComponent
{
    private readonly IProductShippingDimensionService _dimensionService;
    private readonly IRepository<ProductAttributeMapping> _productAttributeMappingRepository;
    private readonly IRepository<ProductAttributeValue> _productAttributeValueRepository;

    public HoodAttributeValueShippingDimensionViewComponent(
        IProductShippingDimensionService dimensionService,
        IRepository<ProductAttributeMapping> productAttributeMappingRepository,
        IRepository<ProductAttributeValue> productAttributeValueRepository)
    {
        _dimensionService = dimensionService;
        _productAttributeMappingRepository = productAttributeMappingRepository;
        _productAttributeValueRepository = productAttributeValueRepository;
    }

    public async Task<IViewComponentResult> InvokeAsync(string widgetZone, object additionalData)
    {
        var valueId = GetInt(additionalData, "Id");
        var productId = GetInt(additionalData, "ProductId");
        var mappingId = GetInt(additionalData, "ProductAttributeMappingId");

        // In ProductAttributeValueEditPopup nopCommerce does not always pass the popup model as widget additionalData.
        // The value id is usually only available in the route: /Admin/Product/ProductAttributeValueEditPopup/{id}
        if (valueId <= 0)
            valueId = TryResolveAttributeValueIdFromRequest();

        if ((productId <= 0 || mappingId <= 0) && valueId > 0)
        {
            var value = await _productAttributeValueRepository.GetByIdAsync(valueId);
            if (value != null)
                mappingId = value.ProductAttributeMappingId;
        }

        if (productId <= 0 && mappingId > 0)
        {
            var mapping = await _productAttributeMappingRepository.GetByIdAsync(mappingId);
            productId = mapping?.ProductId ?? 0;
        }

        var model = new AttributeValueShippingDimensionEditorModel
        {
            ProductId = productId,
            ProductAttributeValueId = valueId,
            Divisor = 5000m,
            PackageCount = 1,
            RuleType = "ATTRIBUTE_VALUE"
        };

        if (productId > 0 && valueId > 0)
        {
            var rules = await _dimensionService.GetRulesByProductIdAsync(productId, activeOnly: false);
            var rule = rules.FirstOrDefault(r => r.ProductAttributeValueId == valueId
                && (string.Equals(r.RuleType, "ARROW_PCS", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(r.RuleType, "ATTRIBUTE_VALUE", StringComparison.OrdinalIgnoreCase)));

            if (rule != null)
            {
                model.ExistingRuleId = rule.Id;
                model.Enabled = rule.IsActive;
                model.RuleType = string.IsNullOrWhiteSpace(rule.RuleType) ? "ATTRIBUTE_VALUE" : rule.RuleType;
                model.LengthCm = rule.LengthCm;
                model.WidthCm = rule.WidthCm;
                model.HeightCm = rule.HeightCm;
                model.WeightGram = rule.WeightGram;
                model.Divisor = rule.Divisor <= 0 ? 5000m : rule.Divisor;
                model.PackageCount = rule.PackageCount <= 0 ? 1 : rule.PackageCount;
                model.IsShipSeparately = rule.IsShipSeparately;
                model.Source = rule.Source;
            }
        }

        return View("~/Plugins/Shipping.FixedByWeightByTotal/Views/Components/HoodAttributeValueShippingDimension/Default.cshtml", model);
    }

    private int TryResolveAttributeValueIdFromRequest()
    {
        var request = ViewComponentContext?.ViewContext?.HttpContext?.Request;
        if (request == null)
            return 0;

        // Normal MVC route value
        if (request.RouteValues.TryGetValue("id", out var routeId) && int.TryParse(routeId?.ToString(), out var idFromRoute))
            return idFromRoute;

        // Fallback for /Admin/Product/ProductAttributeValueEditPopup/743?...
        var path = request.Path.Value ?? string.Empty;
        var marker = "/ProductAttributeValueEditPopup/";
        var index = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index >= 0)
        {
            var tail = path[(index + marker.Length)..];
            var numberText = new string(tail.TakeWhile(char.IsDigit).ToArray());
            if (int.TryParse(numberText, out var idFromPath))
                return idFromPath;
        }

        return 0;
    }

    private static int GetInt(object data, string propertyName)
    {
        if (data == null)
            return 0;

        var prop = data.GetType().GetProperty(propertyName);
        if (prop == null)
            return 0;

        var value = prop.GetValue(data);
        if (value == null)
            return 0;

        return int.TryParse(value.ToString(), out var i) ? i : 0;
    }
}
