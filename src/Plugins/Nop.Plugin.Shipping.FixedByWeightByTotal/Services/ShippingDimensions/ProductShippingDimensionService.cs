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
    private readonly IRepository<HoodProductShippingDriverAttribute> _driverAttributeRepository;
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
        IRepository<HoodProductShippingDriverAttribute> driverAttributeRepository,
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
        _driverAttributeRepository = driverAttributeRepository;
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

    public async Task SaveAttributeValueDimensionAsync(int productId, int productAttributeValueId, bool enabled, string ruleType, decimal? lengthCm, decimal? widthCm, decimal? heightCm, decimal? weightGram, decimal divisor, int packageCount, bool isShipSeparately)
    {
        if (productAttributeValueId <= 0)
            return;

        if (productId <= 0)
        {
            var value = await _productAttributeValueRepository.GetByIdAsync(productAttributeValueId);
            if (value != null)
            {
                var mapping = await _productAttributeMappingRepository.GetByIdAsync(value.ProductAttributeMappingId);
                productId = mapping?.ProductId ?? 0;
            }
        }

        if (productId <= 0)
            return;

        var rules = await GetRulesByProductIdAsync(productId, activeOnly: false);
        var rule = rules.FirstOrDefault(r => r.ProductAttributeValueId == productAttributeValueId
            && (string.Equals(r.RuleType, "ARROW_PCS", StringComparison.OrdinalIgnoreCase)
                || string.Equals(r.RuleType, "ATTRIBUTE_VALUE", StringComparison.OrdinalIgnoreCase)));

        if (!enabled)
        {
            if (rule != null)
            {
                rule.IsActive = false;
                rule.UpdatedOnUtc = DateTime.UtcNow;
                await UpdateRuleAsync(rule);
            }
            return;
        }

        var l = lengthCm ?? 0;
        var w = widthCm ?? 0;
        var h = heightCm ?? 0;
        if (l <= 0 || w <= 0 || h <= 0)
            return;

        rule ??= new HoodProductShippingDimensionRule
        {
            ProductId = productId,
            ProductAttributeValueId = productAttributeValueId,
            CreatedOnUtc = DateTime.UtcNow
        };

        rule.ProductId = productId;
        rule.ProductAttributeValueId = productAttributeValueId;
        rule.AttributeValueIdsCsv = null;
        rule.AttributeHash = null;
        rule.RuleType = string.IsNullOrWhiteSpace(ruleType) ? "ATTRIBUTE_VALUE" : ruleType.Trim().ToUpperInvariant();
        rule.LengthCm = l;
        rule.WidthCm = w;
        rule.HeightCm = h;
        rule.WeightGram = weightGram;
        rule.Divisor = divisor <= 0 ? GetDivisor() : divisor;
        rule.PackageCount = packageCount <= 0 ? 1 : packageCount;
        rule.IsShipSeparately = isShipSeparately;
        rule.IsActive = true;
        rule.Source = "ProductAttributeValue popup";
        rule.Confidence = "MANUAL";

        if (rule.Id > 0)
            await UpdateRuleAsync(rule);
        else
            await InsertRuleAsync(rule);
    }

    public string BuildAttributeValueIdsCsv(IEnumerable<int> attributeValueIds)
    {
        return string.Join(',', attributeValueIds.Distinct().OrderBy(id => id));
    }

    public string BuildAttributeHash(string attributeValueIdsCsv)
    {
        var normalized = BuildAttributeValueIdsCsv(ParseCsvIds(attributeValueIdsCsv));
        return ComputeSha256Hex(normalized);
    }

    private static string ComputeSha256Hex(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty));
        return Convert.ToHexString(bytes);
    }



    public async Task<IList<ShippingDriverAttributeModel>> GetProductAttributeDriverOptionsAsync(int productId)
    {
        if (productId <= 0)
            return new List<ShippingDriverAttributeModel>();

        var tableCheck = await _dataProvider.QueryAsync<TableExistsResult>(@"
SELECT CASE WHEN OBJECT_ID(N'dbo.HoodProductShippingDriverAttribute', N'U') IS NULL THEN 0 ELSE 1 END AS ExistsFlag");

        var hasDriverTable = tableCheck.FirstOrDefault()?.ExistsFlag == 1;

        var sql = hasDriverTable
            ? $@"
SELECT
    ISNULL(D.Id, 0) AS Id,
    PAM.ProductId,
    PAM.Id AS ProductAttributeMappingId,
    PA.Id AS ProductAttributeId,
    PA.Name AS ProductAttributeName,
    CAST(PAM.AttributeControlTypeId AS nvarchar(50)) AS ControlTypeName,
    ISNULL(D.RuleType, N'ATTRIBUTE_VALUE') AS RuleType,
    CAST(ISNULL(D.IsActive, 0) AS bit) AS IsActive,
    CAST(CASE WHEN D.Id IS NULL THEN 0 ELSE 1 END AS bit) AS IsConfigured
FROM dbo.Product_ProductAttribute_Mapping PAM
JOIN dbo.ProductAttribute PA ON PA.Id = PAM.ProductAttributeId
LEFT JOIN dbo.HoodProductShippingDriverAttribute D
    ON D.ProductAttributeMappingId = PAM.Id
WHERE PAM.ProductId = {productId}
ORDER BY PAM.DisplayOrder, PA.Name;"
            : $@"
SELECT
    CAST(0 AS int) AS Id,
    PAM.ProductId,
    PAM.Id AS ProductAttributeMappingId,
    PA.Id AS ProductAttributeId,
    PA.Name AS ProductAttributeName,
    CAST(PAM.AttributeControlTypeId AS nvarchar(50)) AS ControlTypeName,
    CAST(N'ATTRIBUTE_VALUE' AS nvarchar(50)) AS RuleType,
    CAST(0 AS bit) AS IsActive,
    CAST(0 AS bit) AS IsConfigured
FROM dbo.Product_ProductAttribute_Mapping PAM
JOIN dbo.ProductAttribute PA ON PA.Id = PAM.ProductAttributeId
WHERE PAM.ProductId = {productId}
ORDER BY PAM.DisplayOrder, PA.Name;";

        return await _dataProvider.QueryAsync<ShippingDriverAttributeModel>(sql);
    }

    public async Task SaveShippingDriverAttributeAsync(int productId, int productAttributeMappingId, string ruleType, bool isActive = true)
    {
        if (productId <= 0 || productAttributeMappingId <= 0)
            return;

        var mapping = await _productAttributeMappingRepository.GetByIdAsync(productAttributeMappingId);
        if (mapping == null || mapping.ProductId != productId)
            return;

        var productAttribute = await _productAttributeRepository.GetByIdAsync(mapping.ProductAttributeId);
        var existing = (await _driverAttributeRepository.GetAllAsync(query => query.Where(x => x.ProductAttributeMappingId == productAttributeMappingId)))
            .FirstOrDefault();

        existing ??= new HoodProductShippingDriverAttribute
        {
            ProductId = productId,
            ProductAttributeMappingId = productAttributeMappingId,
            CreatedOnUtc = DateTime.UtcNow
        };

        existing.ProductId = productId;
        existing.ProductAttributeMappingId = productAttributeMappingId;
        existing.ProductAttributeId = mapping.ProductAttributeId;
        existing.ProductAttributeName = productAttribute?.Name;
        existing.RuleType = string.IsNullOrWhiteSpace(ruleType) ? "ATTRIBUTE_VALUE" : ruleType.Trim().ToUpperInvariant();
        existing.IsActive = isActive;
        existing.UpdatedOnUtc = DateTime.UtcNow;

        if (existing.Id > 0)
            await _driverAttributeRepository.UpdateAsync(existing, false);
        else
            await _driverAttributeRepository.InsertAsync(existing, false);
    }

    public async Task DeleteShippingDriverAttributeAsync(int id)
    {
        var item = await _driverAttributeRepository.GetByIdAsync(id);
        if (item != null)
            await _driverAttributeRepository.DeleteAsync(item, false);
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
        var attributesXmlHash = ComputeSha256Hex(shoppingCartItem.AttributesXml ?? string.Empty);
        var emptyHash = ComputeSha256Hex(string.Empty);
        var rules = await GetRulesByProductIdAsync(product.Id);

        var rule = rules.FirstOrDefault(r =>
                string.Equals(r.RuleType, "ATTRIBUTE_COMBINATION", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(r.AttributeHash) &&
                !string.Equals(r.AttributeHash, emptyHash, StringComparison.OrdinalIgnoreCase) &&
                (string.Equals(r.AttributeHash, attributesXmlHash, StringComparison.OrdinalIgnoreCase)
                 || string.Equals(r.AttributeHash, selectedHash, StringComparison.OrdinalIgnoreCase)))
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
        // PackageCount means this cart item ships as N identical packages.
        // The chargeable weight is calculated per package, then multiplied by package count and cart quantity.
        measure.RateLookupWeight = measure.ChargeableWeightKg * measure.PackageCount * multiplier * shoppingCartItem.Quantity;

        return measure;
    }

    public async Task<IList<ShippingLearningSuggestionModel>> GetLearningSuggestionsAsync(int minimumSampleCount = 1)
    {
        var existsRows = await _dataProvider.QueryAsync<TableExistsResult>(@"
SELECT CASE
    WHEN OBJECT_ID(N'dbo.NavlungoProductMeasureStatsSafe', N'U') IS NOT NULL
     AND OBJECT_ID(N'dbo.NavlungoOrderMatchFinal', N'U') IS NOT NULL
     AND OBJECT_ID(N'dbo.NavlungoPackages_20260515', N'U') IS NOT NULL
    THEN 1 ELSE 0 END AS ExistsFlag");

        if (existsRows.FirstOrDefault()?.ExistsFlag != 1)
            return new List<ShippingLearningSuggestionModel>();

        var min = Math.Max(1, minimumSampleCount);

        // Returns two suggestion layers:
        // 1) ATTRIBUTE_COMBINATION from prepared NavlungoProductMeasureStatsSafe.
        // 2) ARROW_PCS directly from trusted matched shipments by parsing the Pcs/ProductAttributeValueId from OrderItem.AttributesXml.
        // ARROW_PCS suggestions can be applied to ProductAttributeValue popup fields automatically.
        var sql = $@"
;WITH ProductAttributeCounts AS
(
    SELECT
        ProductId,
        COUNT(*) AS AttributeMappingCount
    FROM dbo.Product_ProductAttribute_Mapping
    GROUP BY ProductId
),
CombinationSuggestions AS
(
    SELECT
        S.ProductId,
        P.Name AS ProductName,
        CAST(NULL AS int) AS ProductAttributeValueId,
        CAST(NULL AS nvarchar(400)) AS ProductAttributeValueName,
        CASE
            WHEN ISNULL(PAC.AttributeMappingCount, 0) = 0
              OR UPPER(ISNULL(S.AttributeHash, N'')) = N'E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855'
            THEN CAST(NULL AS varchar(64))
            ELSE S.AttributeHash
        END AS AttributeHash,
        CAST(CASE
            WHEN ISNULL(PAC.AttributeMappingCount, 0) = 0
              OR UPPER(ISNULL(S.AttributeHash, N'')) = N'E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855'
            THEN N'PRODUCT_DEFAULT'
            ELSE N'ATTRIBUTE_COMBINATION'
        END AS nvarchar(50)) AS RuleType,
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
    LEFT JOIN ProductAttributeCounts PAC ON PAC.ProductId = S.ProductId
    WHERE S.SampleCount >= {min}
),
OrderLineStats AS
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
        OI.ProductId,
        OI.AttributesXml,
        OI.AttributeDescription
    FROM dbo.NavlungoOrderMatchFinal M
    JOIN OrderLineStats LS
        ON LS.OrderId = M.OrderId
    JOIN dbo.OrderItem OI
        ON OI.OrderId = M.OrderId
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
),
SelectedAttributeValues AS
(
    SELECT
        T.*,
        TRY_CONVERT(INT, V.N.value('(text())[1]', 'nvarchar(100)')) AS ProductAttributeValueId
    FROM TrustedSingleItem T
    CROSS APPLY (SELECT TRY_CONVERT(XML, T.AttributesXml) AS AttributesXmlTyped) AX
    CROSS APPLY AX.AttributesXmlTyped.nodes('/Attributes/ProductAttribute/ProductAttributeValue/Value') V(N)
),
PcsValues AS
(
    SELECT
        SAV.*,
        PA.Name AS AttributeName,
        PAV.Name AS AttributeValueName
    FROM SelectedAttributeValues SAV
    JOIN dbo.ProductAttributeValue PAV
        ON PAV.Id = SAV.ProductAttributeValueId
    JOIN dbo.Product_ProductAttribute_Mapping PAM
        ON PAM.Id = PAV.ProductAttributeMappingId
    JOIN dbo.ProductAttribute PA
        ON PA.Id = PAM.ProductAttributeId
    WHERE
        LOWER(PA.Name) IN (N'pcs', N'adet')
        OR LOWER(PA.Name) LIKE N'%pcs%'
        OR LOWER(PA.Name) LIKE N'%adet%'
),
Pkg AS
(
    SELECT
        P.shipmentUuid,
        P.packageIndex,
        P.lengthCm,
        P.widthCm,
        P.heightCm,
        P.weightKg,
        P.chargeableWeightKg,
        P.volumetricWeightDesi,
        P.calculatedDesi_LxWxH_5000,
        D.LongCm,
        D.ShortCm,
        P.lengthCm + P.widthCm + P.heightCm - D.LongCm - D.ShortCm AS MidCm
    FROM dbo.NavlungoPackages_20260515 P
    CROSS APPLY
    (
        SELECT
            MAX(v) AS LongCm,
            MIN(v) AS ShortCm
        FROM (VALUES (P.lengthCm), (P.widthCm), (P.heightCm)) X(v)
    ) D
    WHERE
        P.lengthCm IS NOT NULL
        AND P.widthCm IS NOT NULL
        AND P.heightCm IS NOT NULL
        AND P.weightKg IS NOT NULL
),
ArrowPcsSuggestions AS
(
    SELECT
        PV.ProductId,
        PR.Name AS ProductName,
        PV.ProductAttributeValueId,
        MAX(PV.AttributeValueName) AS ProductAttributeValueName,
        CAST(NULL AS varchar(64)) AS AttributeHash,
        CAST(N'ARROW_PCS' AS nvarchar(50)) AS RuleType,
        COUNT(*) AS SampleCount,
        MAX(PKG.LongCm) AS SuggestedLengthCm,
        MAX(PKG.MidCm) AS SuggestedWidthCm,
        MAX(PKG.ShortCm) AS SuggestedHeightCm,
        CAST(MAX(PKG.weightKg * 1000.0) AS decimal(18,4)) AS SuggestedWeightGram,
        MAX(PKG.chargeableWeightKg) AS SuggestedChargeableWeightKg,
        CAST(CASE WHEN COUNT(*) >= 3 THEN N'HIGH' WHEN COUNT(*) = 2 THEN N'MEDIUM' ELSE N'REVIEW' END AS nvarchar(50)) AS Confidence,
        MAX(PV.shipmentNo) AS ExampleShipmentNo,
        MAX(PV.OrderId) AS ExampleOrderId,
        MAX(PV.AttributeDescription) AS ExampleAttributeDescription
    FROM PcsValues PV
    JOIN Pkg PKG
        ON PKG.shipmentUuid = PV.shipmentUuid
    LEFT JOIN dbo.Product PR
        ON PR.Id = PV.ProductId
    GROUP BY
        PV.ProductId,
        PR.Name,
        PV.ProductAttributeValueId
    HAVING COUNT(*) >= {min}
),
DriverAttributeSuggestions AS
(
    SELECT
        SAV.ProductId,
        PR.Name AS ProductName,
        SAV.ProductAttributeValueId,
        MAX(PAV.Name) AS ProductAttributeValueName,
        CAST(NULL AS varchar(64)) AS AttributeHash,
        CAST(CASE
            WHEN UPPER(ISNULL(D.RuleType, N'')) IN (N'ARROW_PCS', N'ATTRIBUTE_VALUE') THEN UPPER(D.RuleType)
            ELSE N'ATTRIBUTE_VALUE'
        END AS nvarchar(50)) AS RuleType,
        COUNT(*) AS SampleCount,
        MAX(PKG.LongCm) AS SuggestedLengthCm,
        MAX(PKG.MidCm) AS SuggestedWidthCm,
        MAX(PKG.ShortCm) AS SuggestedHeightCm,
        CAST(MAX(PKG.weightKg * 1000.0) AS decimal(18,4)) AS SuggestedWeightGram,
        MAX(PKG.chargeableWeightKg) AS SuggestedChargeableWeightKg,
        CAST(CASE WHEN COUNT(*) >= 3 THEN N'HIGH' WHEN COUNT(*) = 2 THEN N'MEDIUM' ELSE N'REVIEW' END AS nvarchar(50)) AS Confidence,
        MAX(SAV.shipmentNo) AS ExampleShipmentNo,
        MAX(SAV.OrderId) AS ExampleOrderId,
        MAX(SAV.AttributeDescription) AS ExampleAttributeDescription
    FROM SelectedAttributeValues SAV
    JOIN dbo.ProductAttributeValue PAV
        ON PAV.Id = SAV.ProductAttributeValueId
    JOIN dbo.HoodProductShippingDriverAttribute D
        ON D.ProductId = SAV.ProductId
       AND D.ProductAttributeMappingId = PAV.ProductAttributeMappingId
       AND D.IsActive = 1
    JOIN Pkg PKG
        ON PKG.shipmentUuid = SAV.shipmentUuid
    LEFT JOIN dbo.Product PR
        ON PR.Id = SAV.ProductId
    GROUP BY
        SAV.ProductId,
        PR.Name,
        SAV.ProductAttributeValueId,
        CASE
            WHEN UPPER(ISNULL(D.RuleType, N'')) IN (N'ARROW_PCS', N'ATTRIBUTE_VALUE') THEN UPPER(D.RuleType)
            ELSE N'ATTRIBUTE_VALUE'
        END
    HAVING COUNT(*) >= {min}
)
SELECT *
FROM
(
    SELECT * FROM CombinationSuggestions
    UNION ALL
    SELECT * FROM ArrowPcsSuggestions
    UNION ALL
    SELECT * FROM DriverAttributeSuggestions
) X
ORDER BY
    CASE X.RuleType WHEN N'ARROW_PCS' THEN 1 WHEN N'PRODUCT_DEFAULT' THEN 2 WHEN N'ATTRIBUTE_VALUE' THEN 3 ELSE 4 END,
    X.SampleCount DESC,
    X.SuggestedChargeableWeightKg DESC,
    X.ProductId,
    X.ProductAttributeValueName;";

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

    public async Task<int> ApplyLearningSuggestionsAsync(int minimumSampleCount = 2, bool overwriteExisting = false, string ruleTypeFilter = null)
    {
        var suggestions = await GetLearningSuggestionsAsync(minimumSampleCount);
        var applied = 0;

        var filterSet = string.IsNullOrWhiteSpace(ruleTypeFilter)
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : ruleTypeFilter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => x.Trim().ToUpperInvariant())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (filterSet.Any())
            suggestions = suggestions.Where(s => filterSet.Contains((s.RuleType ?? string.Empty).Trim().ToUpperInvariant())).ToList();

        foreach (var s in suggestions)
        {
            if (await IsProductExcludedFromAutomationAsync(s.ProductId))
                continue;

            var existingRules = await GetRulesByProductIdAsync(s.ProductId, activeOnly: false);
            var existing = existingRules.FirstOrDefault(r =>
            {
                if (!string.Equals(r.RuleType ?? string.Empty, s.RuleType ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                    return false;

                if (string.Equals(s.RuleType, "PRODUCT_DEFAULT", StringComparison.OrdinalIgnoreCase))
                    return true;

                if (s.ProductAttributeValueId.HasValue && s.ProductAttributeValueId.Value > 0)
                    return r.ProductAttributeValueId == s.ProductAttributeValueId.Value;

                if (!string.IsNullOrWhiteSpace(s.AttributeHash))
                    return string.Equals(r.AttributeHash ?? string.Empty, s.AttributeHash, StringComparison.OrdinalIgnoreCase);

                return false;
            });

            if (existing != null && !overwriteExisting)
            {
                if (string.Equals(existing.RuleType, "PRODUCT_DEFAULT", StringComparison.OrdinalIgnoreCase))
                    await ApplyProductDefaultToNativeProductAsync(existing);

                continue;
            }

            var rule = existing ?? new HoodProductShippingDimensionRule
            {
                ProductId = s.ProductId,
                CreatedOnUtc = DateTime.UtcNow
            };

            rule.ProductId = s.ProductId;
            rule.ProductAttributeValueId = s.ProductAttributeValueId;
            rule.AttributeHash = string.IsNullOrWhiteSpace(s.AttributeHash) ? null : s.AttributeHash;
            rule.AttributeValueIdsCsv = null;
            rule.RuleType = string.IsNullOrWhiteSpace(s.RuleType) ? "ATTRIBUTE_COMBINATION" : s.RuleType.Trim().ToUpperInvariant();
            rule.LengthCm = s.SuggestedLengthCm;
            rule.WidthCm = s.SuggestedWidthCm;
            rule.HeightCm = s.SuggestedHeightCm;
            rule.Divisor = GetDivisor();
            // PRODUCT_DEFAULT is also written to native nopCommerce Product.Weight.
            // Native FixedByWeight paths do not understand dimensional weight, so for product-level
            // defaults store the billable/effective weight: max(actual weight, volumetric weight).
            rule.WeightGram = string.Equals(rule.RuleType, "PRODUCT_DEFAULT", StringComparison.OrdinalIgnoreCase)
                ? GetEffectiveBillingWeightGram(s, rule.LengthCm, rule.WidthCm, rule.HeightCm, rule.Divisor)
                : s.SuggestedWeightGram;
            rule.PackageCount = 1;
            rule.IsShipSeparately = string.Equals(rule.RuleType, "ARROW_PCS", StringComparison.OrdinalIgnoreCase) || rule.IsShipSeparately;
            rule.IsActive = true;
            rule.SampleCount = s.SampleCount;
            rule.Confidence = s.Confidence;
            rule.Source = string.Equals(rule.RuleType, "ARROW_PCS", StringComparison.OrdinalIgnoreCase)
                ? "Navlungo Learning - ARROW_PCS"
                : string.Equals(rule.RuleType, "PRODUCT_DEFAULT", StringComparison.OrdinalIgnoreCase)
                    ? "Navlungo Learning - PRODUCT_DEFAULT"
                    : "Navlungo Learning";
            rule.ExampleShipmentNo = s.ExampleShipmentNo;
            rule.ExampleOrderId = s.ExampleOrderId;

            if (existing == null)
                await InsertRuleAsync(rule);
            else
                await UpdateRuleAsync(rule);

            if (string.Equals(rule.RuleType, "PRODUCT_DEFAULT", StringComparison.OrdinalIgnoreCase))
                await ApplyProductDefaultToNativeProductAsync(rule);

            applied++;
        }

        return applied;
    }


    public async Task<int> ApplyLearningProductProfileAsync(int productId, int minimumSampleCount = 1, bool overwriteExisting = false, string profileType = null)
    {
        if (productId <= 0)
            return 0;

        var suggestions = (await GetLearningSuggestionsAsync(Math.Max(1, minimumSampleCount)))
            .Where(s => s.ProductId == productId)
            .ToList();

        if (!suggestions.Any())
            return 0;

        if (await IsProductExcludedFromAutomationAsync(productId))
            return 0;

        var normalizedProfile = string.IsNullOrWhiteSpace(profileType)
            ? "ALL"
            : profileType.Trim().ToUpperInvariant();

        if (normalizedProfile == "PRODUCT_DEFAULT")
        {
            var maxChargeable = suggestions
                .OrderByDescending(s => s.SuggestedChargeableWeightKg)
                .ThenByDescending(s => s.SuggestedWeightGram)
                .First();

            var productDefault = new ShippingLearningSuggestionModel
            {
                ProductId = productId,
                ProductName = maxChargeable.ProductName,
                ProductAttributeValueId = null,
                ProductAttributeValueName = null,
                AttributeHash = null,
                RuleType = "PRODUCT_DEFAULT",
                SampleCount = suggestions.Sum(s => Math.Max(1, s.SampleCount)),
                // Use the exact package dimensions from the shipment with the highest chargeable kg.
                // Do not combine max length from one variant + max width from another variant; that creates an artificial oversized box.
                SuggestedLengthCm = maxChargeable.SuggestedLengthCm,
                SuggestedWidthCm = maxChargeable.SuggestedWidthCm,
                SuggestedHeightCm = maxChargeable.SuggestedHeightCm,
                // ApplyOneLearningSuggestionAsync will convert this to billable/effective gram for PRODUCT_DEFAULT.
                SuggestedWeightGram = maxChargeable.SuggestedWeightGram,
                SuggestedChargeableWeightKg = maxChargeable.SuggestedChargeableWeightKg,
                Confidence = suggestions.Any(s => string.Equals(s.Confidence, "HIGH", StringComparison.OrdinalIgnoreCase))
                    ? "HIGH"
                    : suggestions.Any(s => string.Equals(s.Confidence, "MEDIUM", StringComparison.OrdinalIgnoreCase))
                        ? "MEDIUM"
                        : "REVIEW",
                ExampleShipmentNo = maxChargeable.ExampleShipmentNo,
                ExampleOrderId = maxChargeable.ExampleOrderId,
                ExampleAttributeDescription = maxChargeable.ExampleAttributeDescription
            };

            return await ApplyOneLearningSuggestionAsync(productDefault, overwriteExisting, "Navlungo Learning - PRODUCT_PROFILE_DEFAULT");
        }

        IEnumerable<ShippingLearningSuggestionModel> filtered = suggestions;
        if (normalizedProfile != "ALL")
        {
            filtered = suggestions.Where(s => string.Equals(s.RuleType, normalizedProfile, StringComparison.OrdinalIgnoreCase));

            // A product configured as ATTRIBUTE_VALUE may also have ARROW_PCS suggestions.
            // Keep ARROW_PCS separate when specifically selected, but allow ALL to apply all layers.
        }

        var applied = 0;
        foreach (var suggestion in filtered)
            applied += await ApplyOneLearningSuggestionAsync(suggestion, overwriteExisting, $"Navlungo Learning - PRODUCT_PROFILE_{normalizedProfile}");

        return applied;
    }

    private async Task<int> ApplyOneLearningSuggestionAsync(ShippingLearningSuggestionModel s, bool overwriteExisting, string sourceOverride = null)
    {
        if (s == null || s.ProductId <= 0)
            return 0;

        if (await IsProductExcludedFromAutomationAsync(s.ProductId))
            return 0;

        var ruleType = string.IsNullOrWhiteSpace(s.RuleType)
            ? "ATTRIBUTE_COMBINATION"
            : s.RuleType.Trim().ToUpperInvariant();

        var existingRules = await GetRulesByProductIdAsync(s.ProductId, activeOnly: false);
        var existing = existingRules.FirstOrDefault(r =>
        {
            if (!string.Equals(r.RuleType ?? string.Empty, ruleType, StringComparison.OrdinalIgnoreCase))
                return false;

            if (string.Equals(ruleType, "PRODUCT_DEFAULT", StringComparison.OrdinalIgnoreCase))
                return true;

            if (s.ProductAttributeValueId.HasValue && s.ProductAttributeValueId.Value > 0)
                return r.ProductAttributeValueId == s.ProductAttributeValueId.Value;

            if (!string.IsNullOrWhiteSpace(s.AttributeHash))
                return string.Equals(r.AttributeHash ?? string.Empty, s.AttributeHash, StringComparison.OrdinalIgnoreCase);

            return false;
        });

        if (existing != null && !overwriteExisting)
        {
            if (string.Equals(existing.RuleType, "PRODUCT_DEFAULT", StringComparison.OrdinalIgnoreCase))
                await ApplyProductDefaultToNativeProductAsync(existing);

            return 0;
        }

        var rule = existing ?? new HoodProductShippingDimensionRule
        {
            ProductId = s.ProductId,
            CreatedOnUtc = DateTime.UtcNow
        };

        rule.ProductId = s.ProductId;
        rule.ProductAttributeValueId = s.ProductAttributeValueId;
        rule.AttributeHash = string.IsNullOrWhiteSpace(s.AttributeHash) ? null : s.AttributeHash;
        rule.AttributeValueIdsCsv = null;
        rule.RuleType = ruleType;
        rule.LengthCm = s.SuggestedLengthCm;
        rule.WidthCm = s.SuggestedWidthCm;
        rule.HeightCm = s.SuggestedHeightCm;
        rule.Divisor = GetDivisor();
        // PRODUCT_DEFAULT can be used by native nopCommerce fixed-weight calculation paths.
        // Therefore store billable/effective gram for product defaults: max(actual, L*W*H/divisor).
        rule.WeightGram = string.Equals(ruleType, "PRODUCT_DEFAULT", StringComparison.OrdinalIgnoreCase)
            ? GetEffectiveBillingWeightGram(s, rule.LengthCm, rule.WidthCm, rule.HeightCm, rule.Divisor)
            : s.SuggestedWeightGram;
        rule.PackageCount = 1;
        rule.IsShipSeparately = string.Equals(ruleType, "ARROW_PCS", StringComparison.OrdinalIgnoreCase) || rule.IsShipSeparately;
        rule.IsActive = true;
        rule.SampleCount = s.SampleCount;
        rule.Confidence = s.Confidence;
        rule.Source = !string.IsNullOrWhiteSpace(sourceOverride)
            ? sourceOverride
            : string.Equals(ruleType, "ARROW_PCS", StringComparison.OrdinalIgnoreCase)
                ? "Navlungo Learning - ARROW_PCS"
                : string.Equals(ruleType, "PRODUCT_DEFAULT", StringComparison.OrdinalIgnoreCase)
                    ? "Navlungo Learning - PRODUCT_DEFAULT"
                    : "Navlungo Learning";
        rule.ExampleShipmentNo = s.ExampleShipmentNo;
        rule.ExampleOrderId = s.ExampleOrderId;

        if (existing == null)
            await InsertRuleAsync(rule);
        else
            await UpdateRuleAsync(rule);

        if (string.Equals(rule.RuleType, "PRODUCT_DEFAULT", StringComparison.OrdinalIgnoreCase))
            await ApplyProductDefaultToNativeProductAsync(rule);

        return 1;
    }

    private decimal GetEffectiveBillingWeightGram(ShippingLearningSuggestionModel suggestion, decimal lengthCm, decimal widthCm, decimal heightCm, decimal divisor)
    {
        if (suggestion == null)
            return 0m;

        var actualWeightGram = Math.Max(0m, suggestion.SuggestedWeightGram);
        var navlungoChargeableGram = suggestion.SuggestedChargeableWeightKg > 0
            ? suggestion.SuggestedChargeableWeightKg * 1000m
            : 0m;
        var dimensionalWeightGram = divisor > 0 && lengthCm > 0 && widthCm > 0 && heightCm > 0
            ? (lengthCm * widthCm * heightCm / divisor) * 1000m
            : 0m;

        return Math.Max(actualWeightGram, Math.Max(navlungoChargeableGram, dimensionalWeightGram));
    }

    private async Task ApplyProductDefaultToNativeProductAsync(HoodProductShippingDimensionRule rule)
    {
        if (rule == null || !string.Equals(rule.RuleType, "PRODUCT_DEFAULT", StringComparison.OrdinalIgnoreCase))
            return;

        var product = await _productService.GetProductByIdAsync(rule.ProductId);
        if (product == null)
            return;

        var divisor = rule.Divisor > 0 ? rule.Divisor : GetDivisor();
        var dimensionalWeightGram = divisor > 0 && rule.LengthCm > 0 && rule.WidthCm > 0 && rule.HeightCm > 0
            ? (rule.LengthCm * rule.WidthCm * rule.HeightCm / divisor) * 1000m
            : 0m;
        var actualOrLearnedWeightGram = rule.WeightGram.HasValue && rule.WeightGram.Value >= 0 ? rule.WeightGram.Value : 0m;
        var nativeBillingWeightGram = Math.Max(actualOrLearnedWeightGram, dimensionalWeightGram);

        // Native nopCommerce Product.Weight is weight-only. For product defaults we write effective/billable weight
        // so dimensional-weight products are not undercharged in native paths or any fallback path.
        if (nativeBillingWeightGram >= 0)
            product.Weight = nativeBillingWeightGram;

        if (rule.LengthCm > 0)
            product.Length = rule.LengthCm;

        if (rule.WidthCm > 0)
            product.Width = rule.WidthCm;

        if (rule.HeightCm > 0)
            product.Height = rule.HeightCm;

        product.ShipSeparately = rule.IsShipSeparately;

        await _productService.UpdateProductAsync(product);
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
                    if (string.Equals(existing.RuleType, "PRODUCT_DEFAULT", StringComparison.OrdinalIgnoreCase))
                        await ApplyProductDefaultToNativeProductAsync(existing);
                    result.Updated++;
                }
                else
                {
                    await InsertRuleAsync(targetRule);
                    if (string.Equals(targetRule.RuleType, "PRODUCT_DEFAULT", StringComparison.OrdinalIgnoreCase))
                        await ApplyProductDefaultToNativeProductAsync(targetRule);
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
        {
            link.CarrierTrackingUrl = BuildCarrierTrackingUrl(!string.IsNullOrWhiteSpace(link.CarrierTrackingNo) ? link.CarrierTrackingNo : link.NavlungoTrackingNo);
        }

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

        // Navlungo numbers are not reliably indexed by 17track. Use Navlungo public tracking page.
        if (t.StartsWith("NVL", StringComparison.OrdinalIgnoreCase))
            return $"https://navlungo.com/track?carrier=nvl&trackingNumber={u}";

        if (t.StartsWith("1Z", StringComparison.OrdinalIgnoreCase))
            return $"https://www.ups.com/track?tracknum={u}";

        if (t.StartsWith("R", StringComparison.OrdinalIgnoreCase) || t.StartsWith("C", StringComparison.OrdinalIgnoreCase) || t.EndsWith("TR", StringComparison.OrdinalIgnoreCase))
            return $"https://gonderitakip.ptt.gov.tr/Track/Verify?q={u}";

        return $"https://navlungo.com/track?carrier=nvl&trackingNumber={u}";
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
