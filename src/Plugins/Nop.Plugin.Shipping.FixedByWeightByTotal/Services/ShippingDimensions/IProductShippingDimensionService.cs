using Nop.Core.Domain.Orders;
using Nop.Plugin.Shipping.FixedByWeightByTotal.Domain;
using Nop.Plugin.Shipping.FixedByWeightByTotal.Models.ShippingDimensions;

namespace Nop.Plugin.Shipping.FixedByWeightByTotal.Services.ShippingDimensions;

public interface IProductShippingDimensionService
{
    Task<IList<HoodProductShippingDimensionRule>> GetRulesByProductIdAsync(int productId, bool activeOnly = true);
    Task<HoodProductShippingDimensionRule> GetRuleByIdAsync(int id);
    Task InsertRuleAsync(HoodProductShippingDimensionRule rule);
    Task UpdateRuleAsync(HoodProductShippingDimensionRule rule);
    Task DeleteRuleAsync(HoodProductShippingDimensionRule rule);
    Task SaveAttributeValueDimensionAsync(int productId, int productAttributeValueId, bool enabled, string ruleType, decimal? lengthCm, decimal? widthCm, decimal? heightCm, decimal? weightGram, decimal divisor, int packageCount, bool isShipSeparately);

    Task<IList<ShippingLearningSuggestionModel>> GetLearningSuggestionsAsync(int minimumSampleCount = 1);
    Task<IList<ShippingDriverAttributeModel>> GetProductAttributeDriverOptionsAsync(int productId);
    Task SaveShippingDriverAttributeAsync(int productId, int productAttributeMappingId, string ruleType, bool isActive = true);
    Task DeleteShippingDriverAttributeAsync(int id);
    Task<IList<ShippingLearningDetailModel>> GetLearningSuggestionDetailsAsync(int productId, string attributeHash = null, int? productAttributeValueId = null, string ruleType = null);
    Task<int> ApplyLearningSuggestionsAsync(int minimumSampleCount = 2, bool overwriteExisting = false, string ruleTypeFilter = null);
    Task<int> ApplyLearningProductProfileAsync(int productId, int minimumSampleCount = 1, bool overwriteExisting = false, string profileType = null);

    Task<IList<HoodProductShippingDimensionExclusion>> GetExclusionsAsync(bool activeOnly = true);
    Task SaveExclusionAsync(int productId, string reason, bool isActive = true);
    Task DeleteExclusionAsync(int id);
    Task<bool> IsProductExcludedFromAutomationAsync(int productId);

    Task<ShippingDimensionBulkApplyResultModel> BulkApplyRulesToCategoryAsync(int categoryId, int sourceProductId, bool includeSubcategories = true, bool overwriteExisting = false);

    Task<IList<NavlungoShipmentLinkModel>> GetShipmentLinksAsync(int? nopShipmentId = null, int? orderId = null, string trackingNumber = null);

    Task<ChargeableShippingMeasure> GetCartItemMeasureAsync(ShoppingCartItem shoppingCartItem);
    string BuildAttributeValueIdsCsv(IEnumerable<int> attributeValueIds);
    string BuildAttributeHash(string attributeValueIdsCsv);
}

public class ChargeableShippingMeasure
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public int PackageCount { get; set; } = 1;

    public decimal LengthCm { get; set; }
    public decimal WidthCm { get; set; }
    public decimal HeightCm { get; set; }
    public decimal WeightGram { get; set; }
    public decimal Divisor { get; set; } = 5000m;

    public decimal ActualWeightKg => WeightGram / 1000m;
    public decimal DimensionalWeightKg => Divisor <= 0 ? 0 : (LengthCm * WidthCm * HeightCm) / Divisor;
    public decimal ChargeableWeightKg => Math.Max(ActualWeightKg, DimensionalWeightKg);

    /// <summary>
    /// Existing FixedByWeightByTotal rate tables in this store are gram-based, so kg is multiplied by 1000 by default.
    /// </summary>
    public decimal RateLookupWeight { get; set; }

    public string Source { get; set; }
    public int? RuleId { get; set; }
}
