using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Orders;
using Nop.Data;
using Nop.Plugin.Shipping.FixedByWeightByTotal.Domain;
using Nop.Plugin.Shipping.FixedByWeightByTotal.Models.ProductionTime;
using Nop.Services.Catalog;
using Nop.Services.Shipping;

namespace Nop.Plugin.Shipping.FixedByWeightByTotal.Services.ProductionTime;

public class ProductProductionTimeService : IProductProductionTimeService
{
    private readonly IRepository<HoodProductProductionTime> _productionTimeRepository;
    private readonly IProductService _productService;
    private readonly IShippingService _shippingService;

    public ProductProductionTimeService(
        IRepository<HoodProductProductionTime> productionTimeRepository,
        IProductService productService,
        IShippingService shippingService)
    {
        _productionTimeRepository = productionTimeRepository;
        _productService = productService;
        _shippingService = shippingService;
    }

    public async Task<HoodProductProductionTime> GetByProductIdAsync(int productId, bool activeOnly = false)
    {
        if (productId <= 0)
            return null;

        try
        {
            var rows = await _productionTimeRepository.GetAllAsync(query =>
            {
                query = query.Where(x => x.ProductId == productId);
                if (activeOnly)
                    query = query.Where(x => x.Enabled);
                return query.OrderByDescending(x => x.Id);
            });

            return rows.FirstOrDefault();
        }
        catch (Exception ex) when (IsMissingProductionTimeTableException(ex))
        {
            // Existing installations may run the new widget before the FluentMigrator update creates the table.
            // Do not break product/admin pages; the manual SQL script or plugin reinstall/update will create the table.
            return null;
        }
    }

    public async Task<ProductProductionTimeModel> GetModelByProductIdAsync(int productId)
    {
        var product = productId > 0 ? await _productService.GetProductByIdAsync(productId) : null;
        var record = await GetByProductIdAsync(productId, activeOnly: false);

        if (record == null)
        {
            return new ProductProductionTimeModel
            {
                ProductId = productId,
                ProductName = product?.Name,
                Enabled = false,
                IsHandmade = true,
                ProductionMinDays = 0,
                ProductionMaxDays = 0,
                ProductionTimeText = string.Empty,
                Message = "This item is handmade/made to order. Production time is shown separately from express shipping transit.",
                DisplayOnProductPage = true,
                IncludeInShippingEstimate = true,
                HasRecord = false,
                IsOrderable = product is { Published: true, Deleted: false, DisableBuyButton: false }
            };
        }

        return ToModel(record, product);
    }

    public async Task SaveAsync(ProductProductionTimeModel model, string source = "Admin manual")
    {
        if (model == null || model.ProductId <= 0)
            return;

        try
        {
        var record = await GetByProductIdAsync(model.ProductId, activeOnly: false);
        record ??= new HoodProductProductionTime
        {
            ProductId = model.ProductId,
            CreatedOnUtc = DateTime.UtcNow
        };

        record.Enabled = model.Enabled;
        record.IsHandmade = model.IsHandmade;
        record.ProductionMinDays = Math.Max(0, model.ProductionMinDays);
        record.ProductionMaxDays = Math.Max(record.ProductionMinDays, Math.Max(0, model.ProductionMaxDays));
        record.ProductionTimeText = string.IsNullOrWhiteSpace(model.ProductionTimeText) ? BuildProductionText(record.ProductionMinDays, record.ProductionMaxDays) : model.ProductionTimeText.Trim();
        record.Message = string.IsNullOrWhiteSpace(model.Message)
            ? "This item is handmade/made to order. Production time is separate from express shipping transit."
            : model.Message.Trim();
        record.DisplayOnProductPage = model.DisplayOnProductPage;
        record.IncludeInShippingEstimate = model.IncludeInShippingEstimate;
        record.ParsedFromDescription = model.ParsedFromDescription;
        record.Source = source;
        record.UpdatedOnUtc = DateTime.UtcNow;

        if (record.Id > 0)
            await _productionTimeRepository.UpdateAsync(record, false);
        else
            await _productionTimeRepository.InsertAsync(record, false);
        }
        catch (Exception ex) when (IsMissingProductionTimeTableException(ex))
        {
            // Table is missing. Keep the site alive; run sql/HoodProductProductionTime.manual.sql once.
            return;
        }
    }

    public async Task<int> GetMaxProductionDaysForCartItemsAsync(IEnumerable<ShoppingCartItem> cartItems)
    {
        var range = await GetProductionDayRangeForCartItemsAsync(cartItems);
        return range.MaxDays;
    }

    public async Task<(int MinDays, int MaxDays)> GetProductionDayRangeForCartItemsAsync(IEnumerable<ShoppingCartItem> cartItems)
    {
        if (cartItems == null)
            return (0, 0);

        HoodProductProductionTime longest = null;

        foreach (var cartItem in cartItems)
        {
            if (cartItem == null)
                continue;

            if (await _shippingService.IsFreeShippingAsync(cartItem))
                continue;

            var record = await GetByProductIdAsync(cartItem.ProductId, activeOnly: true);
            if (record == null || !record.IncludeInShippingEstimate)
                continue;

            var recordMin = Math.Max(0, record.ProductionMinDays);
            var recordMax = Math.Max(recordMin, Math.Max(0, record.ProductionMaxDays));
            if (recordMax <= 0)
                continue;

            if (longest == null)
            {
                longest = record;
                continue;
            }

            var currentMin = Math.Max(0, longest.ProductionMinDays);
            var currentMax = Math.Max(currentMin, Math.Max(0, longest.ProductionMaxDays));

            if (recordMax > currentMax || recordMax == currentMax && recordMin > currentMin)
                longest = record;
        }

        if (longest == null)
            return (0, 0);

        var min = Math.Max(0, longest.ProductionMinDays);
        var max = Math.Max(min, Math.Max(0, longest.ProductionMaxDays));
        return (min, max);
    }

    public async Task<IList<ProductProductionTimeModel>> SearchAsync(int productId = 0, bool onlyEnabled = false, int pageSize = 200)
    {
        IList<HoodProductProductionTime> records;
        try
        {
            records = await _productionTimeRepository.GetAllAsync(query =>
            {
                if (productId > 0)
                    query = query.Where(x => x.ProductId == productId);
                if (onlyEnabled)
                    query = query.Where(x => x.Enabled);
                return query.OrderByDescending(x => x.UpdatedOnUtc ?? x.CreatedOnUtc).ThenBy(x => x.ProductId).Take(Math.Max(1, pageSize));
            });
        }
        catch (Exception ex) when (IsMissingProductionTimeTableException(ex))
        {
            return new List<ProductProductionTimeModel>();
        }

        var result = new List<ProductProductionTimeModel>();
        foreach (var record in records)
        {
            var product = await _productService.GetProductByIdAsync(record.ProductId);
            result.Add(ToModel(record, product));
        }

        return result;
    }

    private static bool IsMissingProductionTimeTableException(Exception ex)
    {
        if (ex == null)
            return false;

        var message = ex.Message ?? string.Empty;
        var mentionsTable = message.IndexOf("HoodProductProductionTime", StringComparison.OrdinalIgnoreCase) >= 0;
        var isInvalidObject = message.IndexOf("Invalid object name", StringComparison.OrdinalIgnoreCase) >= 0
            || message.IndexOf("Geçersiz nesne adı", StringComparison.OrdinalIgnoreCase) >= 0;

        if (mentionsTable && isInvalidObject)
            return true;

        return ex.InnerException != null && IsMissingProductionTimeTableException(ex.InnerException);
    }

    private ProductProductionTimeModel ToModel(HoodProductProductionTime record, Product product)
    {
        return new ProductProductionTimeModel
        {
            Id = record.Id,
            ProductId = record.ProductId,
            ProductName = product?.Name,
            Enabled = record.Enabled,
            IsHandmade = record.IsHandmade,
            ProductionMinDays = record.ProductionMinDays,
            ProductionMaxDays = record.ProductionMaxDays,
            ProductionTimeText = record.ProductionTimeText,
            Message = record.Message,
            DisplayOnProductPage = record.DisplayOnProductPage,
            IncludeInShippingEstimate = record.IncludeInShippingEstimate,
            ParsedFromDescription = record.ParsedFromDescription,
            Source = record.Source,
            HasRecord = true,
            IsOrderable = product is { Published: true, Deleted: false, DisableBuyButton: false }
        };
    }

    private static string BuildProductionText(int minDays, int maxDays)
    {
        minDays = Math.Max(0, minDays);
        maxDays = Math.Max(minDays, maxDays);

        if (maxDays == 0)
            return string.Empty;

        return minDays == maxDays ? $"{maxDays} days" : $"{minDays}-{maxDays} days";
    }

}
