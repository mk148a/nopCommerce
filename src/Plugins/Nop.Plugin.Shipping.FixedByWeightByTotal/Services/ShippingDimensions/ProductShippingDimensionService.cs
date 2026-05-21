using System.Security.Cryptography;
using System.Text;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Orders;
using Nop.Data;
using Nop.Plugin.Shipping.FixedByWeightByTotal.Domain;
using Nop.Plugin.Shipping.FixedByWeightByTotal.Models.ShippingDimensions;
using Nop.Services.Catalog;

namespace Nop.Plugin.Shipping.FixedByWeightByTotal.Services.ShippingDimensions;

public class ProductShippingDimensionService : IProductShippingDimensionService
{
    private readonly FixedByWeightByTotalSettings _settings;
    private readonly IRepository<HoodProductShippingDimensionRule> _ruleRepository;
    private readonly IRepository<HoodProductShippingDimensionExclusion> _exclusionRepository;
    private readonly IRepository<ProductCategory> _productCategoryRepository;
    private readonly IRepository<Category> _categoryRepository;
    private readonly IRepository<Product> _productRepository;
    private readonly IRepository<ProductAttribute> _productAttributeRepository;
    private readonly IRepository<ProductAttributeMapping> _productAttributeMappingRepository;
    private readonly IRepository<ProductAttributeValue> _productAttributeValueRepository;
    private readonly INopDataProvider _dataProvider;
    private readonly IProductService _productService;
    private readonly IProductAttributeParser _productAttributeParser;

    public ProductShippingDimensionService(FixedByWeightByTotalSettings settings,
        IRepository<HoodProductShippingDimensionRule> ruleRepository,
        IRepository<HoodProductShippingDimensionExclusion> exclusionRepository,
        IRepository<ProductCategory> productCategoryRepository,
        IRepository<Category> categoryRepository,
        IRepository<Product> productRepository,
        IRepository<ProductAttribute> productAttributeRepository,
        IRepository<ProductAttributeMapping> productAttributeMappingRepository,
        IRepository<ProductAttributeValue> productAttributeValueRepository,
        INopDataProvider dataProvider,
        IProductService productService,
        IProductAttributeParser productAttributeParser)
    {
        _settings = settings;
        _ruleRepository = ruleRepository;
        _exclusionRepository = exclusionRepository;
        _productCategoryRepository = productCategoryRepository;
        _categoryRepository = categoryRepository;
        _productRepository = productRepository;
        _productAttributeRepository = productAttributeRepository;
        _productAttributeMappingRepository = productAttributeMappingRepository;
        _productAttributeValueRepository = productAttributeValueRepository;
        _dataProvider = dataProvider;
        _productService = productService;
        _productAttributeParser = productAttributeParser;
    }

    public async Task<IList<HoodProductShippingDimensionRule>> GetRulesByProductIdAsync(int productId, bool activeOnly = true)
    {
        return await _ruleRepository.GetAllAsync(query =>
        {
            query = query.Where(r => r.ProductId == productId);
            if (activeOnly)
                query = query.Where(r => r.IsActive);

            return query.OrderByDescending(r => r.RuleType == "ATTRIBUTE_COMBINATION")
                .ThenByDescending(r => r.RuleType == "ARROW_PCS")
                .ThenByDescending(r => r.RuleType == "ATTRIBUTE_VALUE")
                .ThenBy(r => r.Id);
        });
    }

    public async Task<HoodProductShippingDimensionRule> GetRuleByIdAsync(int id)
    {
        return await _ruleRepository.GetByIdAsync(id);
    }

    public async Task InsertRuleAsync(HoodProductShippingDimensionRule rule)
    {
        rule.CreatedOnUtc = rule.CreatedOnUtc == default ? DateTime.UtcNow : rule.CreatedOnUtc;
        rule.AttributeValueIdsCsv = string.IsNullOrWhiteSpace(rule.AttributeValueIdsCsv) ? null : BuildAttributeValueIdsCsv(ParseCsvIds(rule.AttributeValueIdsCsv));
        rule.AttributeHash = string.IsNullOrWhiteSpace(rule.AttributeValueIdsCsv) ? rule.AttributeHash : BuildAttributeHash(rule.AttributeValueIdsCsv);
        rule.RuleType = string.IsNullOrWhiteSpace(rule.RuleType) ? "PRODUCT_DEFAULT" : rule.RuleType;
        rule.Divisor = rule.Divisor <= 0 ? GetDivisor() : rule.Divisor;
        rule.PackageCount = rule.PackageCount <= 0 ? 1 : rule.PackageCount;

        await _ruleRepository.InsertAsync(rule, false);
    }

    public async Task UpdateRuleAsync(HoodProductShippingDimensionRule rule)
    {
        rule.UpdatedOnUtc = DateTime.UtcNow;
        rule.AttributeValueIdsCsv = string.IsNullOrWhiteSpace(rule.AttributeValueIdsCsv) ? null : BuildAttributeValueIdsCsv(ParseCsvIds(rule.AttributeValueIdsCsv));
        rule.AttributeHash = string.IsNullOrWhiteSpace(rule.AttributeValueIdsCsv) ? rule.AttributeHash : BuildAttributeHash(rule.AttributeValueIdsCsv);
        rule.Divisor = rule.Divisor <= 0 ? GetDivisor() : rule.Divisor;
        rule.PackageCount = rule.PackageCount <= 0 ? 1 : rule.PackageCount;

        await _ruleRepository.UpdateAsync(rule, false);
    }

    public async Task DeleteRuleAsync(HoodProductShippingDimensionRule rule)
    {
        await _ruleRepository.DeleteAsync(rule, false);
    }

    public string BuildAttributeValueIdsCsv(IEnumerable<int> attributeValueIds)
    {
        return string.Join(',', attributeValueIds.Distinct().OrderBy(id => id));
    }

    public string BuildAttributeHash(string attributeValueIdsCsv)
    {
        var normalized = BuildAttributeValueIdsCsv(ParseCsvIds(attributeValueIdsCsv));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes);
    }

    public async Task<ChargeableShippingMeasure> GetCartItemMeasureAsync(ShoppingCartItem shoppingCartItem)
    {
        ArgumentNullException.ThrowIfNull(shoppingCartItem);

        var product = await _productService.GetProductByIdAsync(shoppingCartItem.ProductId);
        if (product == null)
            return new ChargeableShippingMeasure { ProductId = shoppingCartItem.ProductId, Quantity = shoppingCartItem.Quantity, Source = "product-not-found" };

        var selectedValues = await _productAttributeParser.ParseProductAttributeValuesAsync(shoppingCartItem.AttributesXml);
        var selectedIds = selectedValues.Select(v => v.Id).Distinct().OrderBy(id => id).ToList();
        var selectedCsv = BuildAttributeValueIdsCsv(selectedIds);
        var selectedHash = BuildAttributeHash(selectedCsv);
        var rules = await GetRulesByProductIdAsync(product.Id);

        var rule = rules.FirstOrDefault(r =>
                string.Equals(r.RuleType, "ATTRIBUTE_COMBINATION", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(r.AttributeHash) &&
                string.Equals(r.AttributeHash, selectedHash, StringComparison.OrdinalIgnoreCase))
            ?? rules.FirstOrDefault(r =>
                r.ProductAttributeValueId.HasValue &&
                selectedIds.Contains(r.ProductAttributeValueId.Value) &&
                string.Equals(r.RuleType, "ARROW_PCS", StringComparison.OrdinalIgnoreCase))
            ?? rules.FirstOrDefault(r =>
                r.ProductAttributeValueId.HasValue &&
                selectedIds.Contains(r.ProductAttributeValueId.Value) &&
                string.Equals(r.RuleType, "ATTRIBUTE_VALUE", StringComparison.OrdinalIgnoreCase))
            ?? rules.FirstOrDefault(r => string.Equals(r.RuleType, "PRODUCT_DEFAULT", StringComparison.OrdinalIgnoreCase));

        decimal nativeWeightGram = product.Weight;
        foreach (var value in selectedValues)
            nativeWeightGram += value.WeightAdjustment;

        var divisor = rule?.Divisor > 0 ? rule.Divisor : GetDivisor();
        var weightGram = rule?.WeightGram ?? nativeWeightGram;
        var length = rule?.LengthCm > 0 ? rule.LengthCm : product.Length;
        var width = rule?.WidthCm > 0 ? rule.WidthCm : product.Width;
        var height = rule?.HeightCm > 0 ? rule.HeightCm : product.Height;
        var packageCount = rule?.PackageCount > 0 ? rule.PackageCount : 1;

        var measure = new ChargeableShippingMeasure
        {
            ProductId = product.Id,
            Quantity = shoppingCartItem.Quantity,
            PackageCount = packageCount,
            LengthCm = length,
            WidthCm = width,
            HeightCm = height,
            WeightGram = Math.Max(weightGram, 0),
            Divisor = divisor,
            Source = rule == null ? "native-product-fallback" : rule.RuleType,
            RuleId = rule?.Id
        };

        var multiplier = _settings.HoodNavlungoRateWeightMultiplier <= 0 ? 1000m : _settings.HoodNavlungoRateWeightMultiplier;
        measure.RateLookupWeight = measure.ChargeableWeightKg * multiplier * shoppingCartItem.Quantity;

        return measure;
    }

    public async Task<IList<ShippingLearningSuggestionModel>> GetLearningSuggestionsAsync(int minimumSampleCount = 1)
    {
        var existsRows = await _dataProvider.QueryAsync<TableExistsResult>(
            "SELECT CASE WHEN OBJECT_ID(N'dbo.NavlungoProductMeasureStatsSafe', N'U') IS NULL THEN 0 ELSE 1 END AS ExistsFlag");

        if (existsRows.FirstOrDefault()?.ExistsFlag != 1)
            return new List<ShippingLearningSuggestionModel>();

        // Reads previously prepared Navlungo analysis table. The admin page can apply these suggestions without manual SQL edits.
        var sql = $@"
SELECT
    S.ProductId,
    P.Name AS ProductName,
    CAST(NULL AS int) AS ProductAttributeValueId,
    CAST(NULL AS nvarchar(400)) AS ProductAttributeValueName,
    S.AttributeHash,
    CAST(N'ATTRIBUTE_COMBINATION' AS nvarchar(50)) AS RuleType,
    S.SampleCount,
    S.SuggestedLengthCm,
    S.SuggestedWidthCm,
    S.SuggestedHeightCm,
    CAST(S.SuggestedActualWeightKg * 1000.0 AS decimal(18,4)) AS SuggestedWeightGram,
    S.SuggestedChargeableWeightKg,
    CAST(CASE WHEN S.SampleCount >= 3 THEN N'HIGH' WHEN S.SampleCount = 2 THEN N'MEDIUM' ELSE N'REVIEW' END AS nvarchar(50)) AS Confidence,
    S.ExampleShipmentNo,
    S.ExampleOrderId,
    S.ExampleAttributeDescription
FROM dbo.NavlungoProductMeasureStatsSafe S
LEFT JOIN dbo.Product P ON P.Id = S.ProductId
WHERE S.SampleCount >= {Math.Max(1, minimumSampleCount)}
ORDER BY S.SampleCount DESC, S.SuggestedChargeableWeightKg DESC;";

        return await _dataProvider.QueryAsync<ShippingLearningSuggestionModel>(sql);
    }

    private class TableExistsResult
    {
        public int ExistsFlag { get; set; }
    }


    public async Task<IList<ShippingLearningDetailModel>> GetLearningSuggestionDetailsAsync(int productId, string attributeHash = null, int? productAttributeValueId = null, string ruleType = null)
    {
        var existsRows = await _dataProvider.QueryAsync<TableExistsResult>(
            "SELECT CASE WHEN OBJECT_ID(N'dbo.NavlungoOrderMatchFinal', N'U') IS NULL OR OBJECT_ID(N'dbo.NavlungoPackages_20260515', N'U') IS NULL THEN 0 ELSE 1 END AS ExistsFlag");

        if (existsRows.FirstOrDefault()?.ExistsFlag != 1)
            return new List<ShippingLearningDetailModel>();

        attributeHash = string.IsNullOrWhiteSpace(attributeHash) ? null : attributeHash.Trim().ToUpperInvariant();
        ruleType = string.IsNullOrWhiteSpace(ruleType) ? null : ruleType.Trim().ToUpperInvariant();

        var filters = new StringBuilder();
        filters.AppendLine($"        AND OI.ProductId = {productId}");

        if (!string.IsNullOrWhiteSpace(attributeHash))
            filters.AppendLine($"        AND CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', CONVERT(NVARCHAR(4000), ISNULL(OI.AttributesXml, N''))), 2) = N'{EscapeSql(attributeHash)}'");

        if (productAttributeValueId.HasValue && productAttributeValueId.Value > 0)
        {
            filters.AppendLine($@"        AND EXISTS
        (
            SELECT 1
            FROM (SELECT TRY_CONVERT(XML, OI.AttributesXml) AS AttributesXmlTyped) AX
            CROSS APPLY AX.AttributesXmlTyped.nodes('/Attributes/ProductAttribute/ProductAttributeValue/Value') V(N)
            WHERE TRY_CONVERT(INT, V.N.value('(text())[1]', 'nvarchar(100)')) = {productAttributeValueId.Value}
        )");
        }

        // The detail page intentionally uses the same trusted sample set as ProductMeasureStatsSafe:
        // safe match + single order item + quantity 1 + single package.
        var sql = $@"
;WITH OrderLineStats AS
(
    SELECT
        OrderId,
        COUNT(*) AS LineCount,
        SUM(Quantity) AS TotalQuantity
    FROM dbo.OrderItem
    GROUP BY OrderId
),
TrustedSingleItem AS
(
    SELECT
        M.shipmentUuid,
        M.shipmentNo,
        M.OrderId,
        M.CustomOrderNumber,
        M.MatchDecision,
        M.Score,
        M.SecondScore2,
        M.ScoreGap,
        M.receiverName,
        M.receiverCountry,
        M.receiverCity,
        M.requestDateTR,
        M.carrierTrackingNo,
        M.navlungoTrackingNo,
        M.navlungoDetailUrl,
        NS.imageLinks AS DocumentsLinks,
        OI.ProductId,
        OI.AttributesXml,
        OI.AttributeDescription,
        CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', CONVERT(NVARCHAR(4000), ISNULL(OI.AttributesXml, N''))), 2) AS AttributeHash
    FROM dbo.NavlungoOrderMatchFinal M
    JOIN OrderLineStats LS
        ON LS.OrderId = M.OrderId
    JOIN dbo.OrderItem OI
        ON OI.OrderId = M.OrderId
    LEFT JOIN dbo.NavlungoShipments_20260515 NS
        ON NS.shipmentUuid = M.shipmentUuid
    WHERE
        M.MatchDecision IN
        (
            'EXACT_TRACKING_SAFE',
            'HIGH_CONFIDENCE_SAFE',
            'PROBABLE_SAFE'
        )
        AND LS.LineCount = 1
        AND LS.TotalQuantity = 1
        AND ISNULL(M.packageCount, 0) = 1
{filters}
)
SELECT
    T.ProductId,
    P.Name AS ProductName,
    T.OrderId,
    T.CustomOrderNumber,
    T.shipmentNo AS ShipmentNo,
    T.shipmentUuid AS ShipmentUuid,
    PKG.packageIndex AS PackageIndex,

    T.receiverName AS ReceiverName,
    T.receiverCountry AS ReceiverCountry,
    T.receiverCity AS ReceiverCity,
    T.requestDateTR AS RequestDateTR,

    T.carrierTrackingNo AS CarrierTrackingNo,
    T.navlungoTrackingNo AS NavlungoTrackingNo,
    T.navlungoDetailUrl AS NavlungoDetailUrl,
    T.DocumentsLinks,
    PKG.packageImageLinks AS PackageImageLinks,

    T.MatchDecision,
    T.Score,
    T.SecondScore2,
    T.ScoreGap,

    T.AttributeHash,
    T.AttributeDescription,

    PKG.lengthCm AS LengthCm,
    PKG.widthCm AS WidthCm,
    PKG.heightCm AS HeightCm,
    PKG.weightKg AS WeightKg,
    CAST(PKG.weightKg * 1000.0 AS decimal(18,4)) AS WeightGram,
    PKG.chargeableWeightKg AS ChargeableWeightKg,
    PKG.volumetricWeightDesi AS VolumetricWeightDesi,
    PKG.calculatedDesi_LxWxH_5000 AS CalculatedDesi
FROM TrustedSingleItem T
JOIN dbo.NavlungoPackages_20260515 PKG
    ON PKG.shipmentUuid = T.shipmentUuid
LEFT JOIN dbo.Product P
    ON P.Id = T.ProductId
WHERE
    PKG.lengthCm IS NOT NULL
    AND PKG.widthCm IS NOT NULL
    AND PKG.heightCm IS NOT NULL
    AND PKG.weightKg IS NOT NULL
ORDER BY
    PKG.chargeableWeightKg DESC,
    PKG.weightKg DESC,
    T.requestDateTR DESC,
    T.shipmentNo;";

        var details = (await _dataProvider.QueryAsync<ShippingLearningDetailModel>(sql)).ToList();
        foreach (var detail in details)
            detail.CarrierTrackingUrl = BuildCarrierTrackingUrl(detail.CarrierTrackingNo);

        return details;
    }

    public async Task<int> ApplyLearningSuggestionsAsync(int minimumSampleCount = 2, bool overwriteExisting = false)
    {
        var suggestions = await GetLearningSuggestionsAsync(minimumSampleCount);
        var applied = 0;

        foreach (var s in suggestions)
        {
            if (await IsProductExcludedFromAutomationAsync(s.ProductId))
                continue;

            var existing = (await GetRulesByProductIdAsync(s.ProductId, activeOnly: false))
                .FirstOrDefault(r => !string.IsNullOrWhiteSpace(s.AttributeHash) && string.Equals(r.AttributeHash, s.AttributeHash, StringComparison.OrdinalIgnoreCase));

            if (existing != null && !overwriteExisting)
                continue;

            var rule = existing ?? new HoodProductShippingDimensionRule
            {
                ProductId = s.ProductId,
                AttributeHash = s.AttributeHash,
                RuleType = s.RuleType,
                CreatedOnUtc = DateTime.UtcNow
            };

            rule.LengthCm = s.SuggestedLengthCm;
            rule.WidthCm = s.SuggestedWidthCm;
            rule.HeightCm = s.SuggestedHeightCm;
            rule.WeightGram = s.SuggestedWeightGram;
            rule.Divisor = GetDivisor();
            rule.PackageCount = 1;
            rule.IsActive = true;
            rule.SampleCount = s.SampleCount;
            rule.Confidence = s.Confidence;
            rule.Source = "Navlungo Learning";
            rule.ExampleShipmentNo = s.ExampleShipmentNo;
            rule.ExampleOrderId = s.ExampleOrderId;

            if (existing == null)
                await InsertRuleAsync(rule);
            else
                await UpdateRuleAsync(rule);

            applied++;
        }

        return applied;
    }


    public async Task<IList<HoodProductShippingDimensionExclusion>> GetExclusionsAsync(bool activeOnly = true)
    {
        return await _exclusionRepository.GetAllAsync(query =>
        {
            if (activeOnly)
                query = query.Where(x => x.IsActive);

            return query.OrderBy(x => x.ProductId).ThenBy(x => x.Id);
        });
    }

    public async Task SaveExclusionAsync(int productId, string reason, bool isActive = true)
    {
        if (productId <= 0)
            return;

        var existing = (await _exclusionRepository.GetAllAsync(query => query.Where(x => x.ProductId == productId)))
            .FirstOrDefault();

        if (existing == null)
        {
            existing = new HoodProductShippingDimensionExclusion
            {
                ProductId = productId,
                CreatedOnUtc = DateTime.UtcNow
            };
        }

        existing.Reason = reason;
        existing.IsActive = isActive;
        existing.UpdatedOnUtc = DateTime.UtcNow;

        if (existing.Id > 0)
            await _exclusionRepository.UpdateAsync(existing, false);
        else
            await _exclusionRepository.InsertAsync(existing, false);
    }

    public async Task DeleteExclusionAsync(int id)
    {
        var item = await _exclusionRepository.GetByIdAsync(id);
        if (item != null)
            await _exclusionRepository.DeleteAsync(item, false);
    }

    public async Task<bool> IsProductExcludedFromAutomationAsync(int productId)
    {
        if (productId <= 0)
            return false;

        var exclusions = await _exclusionRepository.GetAllAsync(query =>
            query.Where(x => x.ProductId == productId && x.IsActive));

        return exclusions.Any();
    }

    public async Task<ShippingDimensionBulkApplyResultModel> BulkApplyRulesToCategoryAsync(int categoryId, int sourceProductId, bool includeSubcategories = true, bool overwriteExisting = false)
    {
        var result = new ShippingDimensionBulkApplyResultModel
        {
            CategoryId = categoryId,
            SourceProductId = sourceProductId
        };

        if (categoryId <= 0 || sourceProductId <= 0)
        {
            result.Messages.Add("CategoryId and SourceProductId are required.");
            return result;
        }

        var sourceRules = (await GetRulesByProductIdAsync(sourceProductId, activeOnly: true)).ToList();
        result.SourceRuleCount = sourceRules.Count;

        if (!sourceRules.Any())
        {
            result.Messages.Add("No active source dimension rules found.");
            return result;
        }

        var targetProductIds = await GetProductIdsByCategoryAsync(categoryId, includeSubcategories);
        targetProductIds.Remove(sourceProductId);
        result.TargetProductCount = targetProductIds.Count;

        foreach (var productId in targetProductIds)
        {
            if (await IsProductExcludedFromAutomationAsync(productId))
            {
                result.Excluded++;
                continue;
            }

            foreach (var sourceRule in sourceRules)
            {
                var targetRule = await BuildCopiedRuleForProductAsync(sourceRule, productId);
                if (targetRule == null)
                {
                    result.Skipped++;
                    continue;
                }

                var existingRules = await GetRulesByProductIdAsync(productId, activeOnly: false);
                var existing = FindEquivalentRule(existingRules, targetRule);

                if (existing != null && !overwriteExisting)
                {
                    result.Skipped++;
                    continue;
                }

                if (existing != null)
                {
                    existing.LengthCm = targetRule.LengthCm;
                    existing.WidthCm = targetRule.WidthCm;
                    existing.HeightCm = targetRule.HeightCm;
                    existing.WeightGram = targetRule.WeightGram;
                    existing.Divisor = targetRule.Divisor;
                    existing.PackageCount = targetRule.PackageCount;
                    existing.IsShipSeparately = targetRule.IsShipSeparately;
                    existing.IsActive = targetRule.IsActive;
                    existing.SampleCount = targetRule.SampleCount;
                    existing.Confidence = targetRule.Confidence;
                    existing.Source = targetRule.Source;
                    existing.ExampleShipmentNo = targetRule.ExampleShipmentNo;
                    existing.ExampleOrderId = targetRule.ExampleOrderId;
                    await UpdateRuleAsync(existing);
                    result.Updated++;
                }
                else
                {
                    await InsertRuleAsync(targetRule);
                    result.Created++;
                }
            }
        }

        result.Messages.Add($"Created: {result.Created}, Updated: {result.Updated}, Skipped: {result.Skipped}, Excluded: {result.Excluded}");
        return result;
    }

    public async Task<IList<NavlungoShipmentLinkModel>> GetShipmentLinksAsync(int? nopShipmentId = null, int? orderId = null, string trackingNumber = null)
    {
        var where = new List<string>();

        if (nopShipmentId.HasValue && nopShipmentId.Value > 0)
            where.Add($"SH.Id = {nopShipmentId.Value}");

        if (orderId.HasValue && orderId.Value > 0)
            where.Add($"O.Id = {orderId.Value}");

        if (!string.IsNullOrWhiteSpace(trackingNumber))
        {
            var normalizedTracking = OnlyAlphaNumUpper(trackingNumber);
            if (!string.IsNullOrWhiteSpace(normalizedTracking))
                where.Add($"dbo.fnOnlyAlphaNumUpper(SH.TrackingNumber) = N'{EscapeSql(normalizedTracking)}'");
        }

        var whereSql = where.Any() ? string.Join(" OR ", where) : "1 = 0";

        var tableCheck = await _dataProvider.QueryAsync<TableExistsResult>(@"
SELECT CASE WHEN OBJECT_ID(N'dbo.NavlungoShipments_20260515', N'U') IS NULL THEN 0 ELSE 1 END AS ExistsFlag");
        if (tableCheck.FirstOrDefault()?.ExistsFlag != 1)
            return new List<NavlungoShipmentLinkModel>();

        var sql = $@"
;WITH Base AS
(
    SELECT
        SH.Id AS NopShipmentId,
        O.Id AS OrderId,
        O.CustomOrderNumber,
        SH.TrackingNumber,
        dbo.fnOnlyAlphaNumUpper(SH.TrackingNumber) AS NopTrackingNorm
    FROM dbo.Shipment SH
    JOIN dbo.[Order] O ON O.Id = SH.OrderId
    WHERE {whereSql}
), MatchByFinal AS
(
    SELECT
        B.NopShipmentId,
        B.OrderId,
        B.CustomOrderNumber,
        N.shipmentNo,
        N.shipmentUuid,
        N.carrierName,
        N.carrierTrackingNo,
        N.navlungoTrackingNo,
        N.navlungoDetailUrl,
        N.imageLinks AS DocumentsLinks,
        N.requestDateTR,
        N.totalActualWeightKg,
        N.totalChargeableWeightKg
    FROM Base B
    JOIN dbo.NavlungoOrderMatchFinal M ON M.OrderId = B.OrderId
    JOIN dbo.NavlungoShipments_20260515 N ON N.shipmentUuid = M.shipmentUuid
), MatchByTracking AS
(
    SELECT
        B.NopShipmentId,
        B.OrderId,
        B.CustomOrderNumber,
        N.shipmentNo,
        N.shipmentUuid,
        N.carrierName,
        N.carrierTrackingNo,
        N.navlungoTrackingNo,
        N.navlungoDetailUrl,
        N.imageLinks AS DocumentsLinks,
        N.requestDateTR,
        N.totalActualWeightKg,
        N.totalChargeableWeightKg
    FROM Base B
    JOIN dbo.NavlungoShipments_20260515 N
        ON B.NopTrackingNorm <> ''
       AND
       (
            B.NopTrackingNorm = N.carrierTrackingNorm
            OR B.NopTrackingNorm = N.navlungoTrackingNorm
            OR N.carrierTrackingNorm LIKE '%' + B.NopTrackingNorm + '%'
            OR B.NopTrackingNorm LIKE '%' + N.carrierTrackingNorm + '%'
       )
), AllMatches AS
(
    SELECT * FROM MatchByFinal
    UNION
    SELECT * FROM MatchByTracking
)
SELECT DISTINCT TOP 50
    A.NopShipmentId,
    A.OrderId,
    A.CustomOrderNumber,
    A.shipmentNo AS ShipmentNo,
    A.shipmentUuid AS ShipmentUuid,
    A.carrierName AS CarrierName,
    A.carrierTrackingNo AS CarrierTrackingNo,
    A.navlungoTrackingNo AS NavlungoTrackingNo,
    A.navlungoDetailUrl AS NavlungoDetailUrl,
    A.DocumentsLinks,
    A.requestDateTR AS RequestDateTR,
    A.totalActualWeightKg AS TotalActualWeightKg,
    A.totalChargeableWeightKg AS TotalChargeableWeightKg,
    STUFF((
        SELECT DISTINCT N' | ' + ISNULL(P.packageImageLinks, N'')
        FROM dbo.NavlungoPackages_20260515 P
        WHERE P.shipmentUuid = A.shipmentUuid
          AND ISNULL(P.packageImageLinks, N'') <> N''
        FOR XML PATH(''), TYPE).value('.', 'nvarchar(max)'), 1, 3, N'') AS PackageImageLinks
FROM AllMatches A
ORDER BY A.requestDateTR DESC;";

        var links = (await _dataProvider.QueryAsync<NavlungoShipmentLinkModel>(sql)).ToList();
        foreach (var link in links)
            link.CarrierTrackingUrl = BuildCarrierTrackingUrl(link.CarrierTrackingNo);

        return links;
    }


    private static string EscapeSql(string value)
    {
        return (value ?? string.Empty).Replace("'", "''");
    }

    private decimal GetDivisor()
    {
        return _settings.HoodNavlungoDimensionalWeightDivisor <= 0 ? 5000m : _settings.HoodNavlungoDimensionalWeightDivisor;
    }


    private async Task<List<int>> GetProductIdsByCategoryAsync(int categoryId, bool includeSubcategories)
    {
        var categoryIds = new HashSet<int> { categoryId };

        if (includeSubcategories)
        {
            var allCategories = await _categoryRepository.GetAllAsync(query => query.Where(c => !c.Deleted));
            var changed = true;
            while (changed)
            {
                changed = false;
                foreach (var cat in allCategories)
                {
                    if (categoryIds.Contains(cat.ParentCategoryId) && categoryIds.Add(cat.Id))
                        changed = true;
                }
            }
        }

        var productCategories = await _productCategoryRepository.GetAllAsync(query => query.Where(pc => categoryIds.Contains(pc.CategoryId)));
        return productCategories.Select(pc => pc.ProductId).Distinct().OrderBy(id => id).ToList();
    }

    private async Task<HoodProductShippingDimensionRule> BuildCopiedRuleForProductAsync(HoodProductShippingDimensionRule sourceRule, int targetProductId)
    {
        var copied = new HoodProductShippingDimensionRule
        {
            ProductId = targetProductId,
            RuleType = sourceRule.RuleType,
            LengthCm = sourceRule.LengthCm,
            WidthCm = sourceRule.WidthCm,
            HeightCm = sourceRule.HeightCm,
            WeightGram = sourceRule.WeightGram,
            Divisor = sourceRule.Divisor,
            PackageCount = sourceRule.PackageCount,
            IsShipSeparately = sourceRule.IsShipSeparately,
            IsActive = true,
            SampleCount = sourceRule.SampleCount,
            Confidence = sourceRule.Confidence,
            Source = $"Bulk copied from product {sourceRule.ProductId}",
            ExampleShipmentNo = sourceRule.ExampleShipmentNo,
            ExampleOrderId = sourceRule.ExampleOrderId,
            CreatedOnUtc = DateTime.UtcNow
        };

        if (sourceRule.ProductAttributeValueId.HasValue && sourceRule.ProductAttributeValueId.Value > 0)
        {
            var targetValueId = await FindMatchingAttributeValueIdAsync(sourceRule.ProductAttributeValueId.Value, targetProductId);
            if (!targetValueId.HasValue)
                return null;

            copied.ProductAttributeValueId = targetValueId.Value;
            copied.AttributeValueIdsCsv = null;
            copied.AttributeHash = null;
            return copied;
        }

        // Attribute-combination hashes cannot be safely copied across different products because ProductAttributeValueId values differ.
        // PRODUCT_DEFAULT rules can be copied as-is.
        if (string.Equals(sourceRule.RuleType, "PRODUCT_DEFAULT", StringComparison.OrdinalIgnoreCase))
            return copied;

        return null;
    }

    private async Task<int?> FindMatchingAttributeValueIdAsync(int sourceProductAttributeValueId, int targetProductId)
    {
        var sourceValue = await _productAttributeValueRepository.GetByIdAsync(sourceProductAttributeValueId);
        if (sourceValue == null)
            return null;

        var sourceMapping = await _productAttributeMappingRepository.GetByIdAsync(sourceValue.ProductAttributeMappingId);
        if (sourceMapping == null)
            return null;

        var sourceAttribute = await _productAttributeRepository.GetByIdAsync(sourceMapping.ProductAttributeId);
        if (sourceAttribute == null)
            return null;

        var targetMappings = await _productAttributeMappingRepository.GetAllAsync(query => query.Where(m => m.ProductId == targetProductId));
        var targetAttributeIds = targetMappings.Select(m => m.ProductAttributeId).Distinct().ToList();
        var targetAttributes = await _productAttributeRepository.GetAllAsync(query => query.Where(a => targetAttributeIds.Contains(a.Id)));
        var matchingAttributeIds = targetAttributes
            .Where(a => string.Equals(NormalizeKey(a.Name), NormalizeKey(sourceAttribute.Name), StringComparison.OrdinalIgnoreCase))
            .Select(a => a.Id)
            .ToHashSet();

        if (!matchingAttributeIds.Any())
            return null;

        var matchingMappings = targetMappings.Where(m => matchingAttributeIds.Contains(m.ProductAttributeId)).Select(m => m.Id).ToList();
        var targetValues = await _productAttributeValueRepository.GetAllAsync(query => query.Where(v => matchingMappings.Contains(v.ProductAttributeMappingId)));

        var sourceValueKey = NormalizeKey(sourceValue.Name);
        return targetValues.FirstOrDefault(v => string.Equals(NormalizeKey(v.Name), sourceValueKey, StringComparison.OrdinalIgnoreCase))?.Id;
    }

    private static HoodProductShippingDimensionRule FindEquivalentRule(IEnumerable<HoodProductShippingDimensionRule> existingRules, HoodProductShippingDimensionRule targetRule)
    {
        return existingRules.FirstOrDefault(r =>
            string.Equals(r.RuleType, targetRule.RuleType, StringComparison.OrdinalIgnoreCase)
            && r.ProductAttributeValueId == targetRule.ProductAttributeValueId
            && string.Equals(r.AttributeHash ?? string.Empty, targetRule.AttributeHash ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            && string.Equals(r.AttributeValueIdsCsv ?? string.Empty, targetRule.AttributeValueIdsCsv ?? string.Empty, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return new string(value.Trim().ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
    }

    private static string OnlyAlphaNumUpper(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return new string(value.ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
    }

    private static string BuildCarrierTrackingUrl(string trackingNumber)
    {
        if (string.IsNullOrWhiteSpace(trackingNumber))
            return string.Empty;

        var t = trackingNumber.Trim();
        var u = Uri.EscapeDataString(t);

        if (t.StartsWith("1Z", StringComparison.OrdinalIgnoreCase))
            return $"https://www.ups.com/track?tracknum={u}";

        if (t.StartsWith("R", StringComparison.OrdinalIgnoreCase) || t.StartsWith("C", StringComparison.OrdinalIgnoreCase) || t.EndsWith("TR", StringComparison.OrdinalIgnoreCase))
            return $"https://gonderitakip.ptt.gov.tr/Track/Verify?q={u}";

        return $"https://www.google.com/search?q={Uri.EscapeDataString(t + " tracking")}";
    }

    private static IEnumerable<int> ParseCsvIds(string csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
            return Array.Empty<int>();

        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var id) ? id : 0)
            .Where(id => id > 0);
    }
}
