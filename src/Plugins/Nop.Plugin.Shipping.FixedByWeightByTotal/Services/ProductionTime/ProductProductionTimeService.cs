using System.Text.RegularExpressions;
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
    private readonly IRepository<Product> _productRepository;
    private readonly IProductService _productService;
    private readonly IShippingService _shippingService;

    public ProductProductionTimeService(
        IRepository<HoodProductProductionTime> productionTimeRepository,
        IRepository<Product> productRepository,
        IProductService productService,
        IShippingService shippingService)
    {
        _productionTimeRepository = productionTimeRepository;
        _productRepository = productRepository;
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
                HasRecord = false
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

    public async Task<ProductProductionTimeModel> ExtractFromDescriptionAsync(int productId, bool save = false)
    {
        var product = await _productService.GetProductByIdAsync(productId);
        if (product == null)
            return new ProductProductionTimeModel { ProductId = productId, Enabled = false };

        var plain = NormalizeDescription($"{product.ShortDescription}\n{product.FullDescription}");
        var parsed = ParseProductionTime(plain);

        var existing = await GetModelByProductIdAsync(productId);
        existing.ProductName = product.Name;
        existing.Enabled = parsed.MaxDays > 0;
        existing.IsHandmade = parsed.IsHandmade || existing.IsHandmade;
        existing.ProductionMinDays = parsed.MinDays;
        existing.ProductionMaxDays = parsed.MaxDays;
        existing.ProductionTimeText = !string.IsNullOrWhiteSpace(parsed.Text)
            ? parsed.Text
            : BuildProductionText(parsed.MinDays, parsed.MaxDays);
        existing.Message = string.IsNullOrWhiteSpace(existing.Message)
            ? "This item is handmade/made to order. Production time is separate from express shipping transit."
            : existing.Message;
        existing.DisplayOnProductPage = true;
        existing.IncludeInShippingEstimate = true;
        existing.ParsedFromDescription = true;
        existing.Source = "Parsed from product description";

        if (save && parsed.MaxDays > 0)
            await SaveAsync(existing, "Parsed from product description");

        return existing;
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

    public async Task<(int Scanned, int Saved, int Skipped, int Errors)> BulkExtractFromDescriptionsAsync(bool onlyMissing = true, int maxProducts = 1000, bool publishedOnly = true)
    {
        var scanned = 0;
        var saved = 0;
        var skipped = 0;
        var errors = 0;

        var products = await _productRepository.GetAllAsync(query =>
        {
            query = query.Where(p => !p.Deleted);
            if (publishedOnly)
                query = query.Where(p => p.Published);

            return query.OrderBy(p => p.Id).Take(Math.Max(1, maxProducts));
        });

        foreach (var product in products)
        {
            scanned++;

            try
            {
                if (onlyMissing)
                {
                    var existing = await GetByProductIdAsync(product.Id, activeOnly: false);
                    if (existing != null)
                    {
                        skipped++;
                        continue;
                    }
                }

                var model = await ExtractFromDescriptionAsync(product.Id, save: true);
                if (model != null && model.Enabled && Math.Max(model.ProductionMinDays, model.ProductionMaxDays) > 0)
                    saved++;
                else
                    skipped++;
            }
            catch
            {
                errors++;
            }
        }

        return (scanned, saved, skipped, errors);
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
            HasRecord = true
        };
    }

    private static string NormalizeDescription(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var text = Regex.Replace(html, "<br\\s*/?>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "</p>|</div>|</li>|</h[1-6]>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "<.*?>", " ", RegexOptions.Singleline);
        text = System.Net.WebUtility.HtmlDecode(text);
        text = Regex.Replace(text, "[ \t]+", " ");
        text = Regex.Replace(text, @"\n\s+", "\n");
        return text.Trim();
    }

    private static (int MinDays, int MaxDays, string Text, bool IsHandmade) ParseProductionTime(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return (0, 0, string.Empty, false);

        var isHandmade = Regex.IsMatch(text, "handmade|hand made|made to order|make to order|custom made|production|processing", RegexOptions.IgnoreCase);
        var candidates = new List<(int Min, int Max, string Original, int Score)>();

        // Strongest signal: content around a Production Time / Processing Time heading.
        foreach (Match heading in Regex.Matches(text, "(?i)(production\\s*time|processing\\s*time|lead\\s*time|make\\s*time|manufactur(?:e|ing)\\s*time).{0,160}"))
        {
            foreach (var parsed in ParseDurations(heading.Value))
                candidates.Add((parsed.Min, parsed.Max, parsed.Text, parsed.Max + 10000));
        }

        // Medium signal: sentences that mention production/make/processing.
        var sentences = Regex.Split(text, @"(?<=[\.\!\?\n])\s+");
        foreach (var sentence in sentences)
        {
            if (!Regex.IsMatch(sentence, "(?i)production|processing|make|made|handmade|hand made|lead time|ready|stock"))
                continue;

            // Avoid interpreting pure shipping/delivery promises as production time.
            if (Regex.IsMatch(sentence, "(?i)delivery|delivered|shipping|ship(?:ping)?") && !Regex.IsMatch(sentence, "(?i)production|processing|make|made|handmade|lead time|stock"))
                continue;

            foreach (var parsed in ParseDurations(sentence))
                candidates.Add((parsed.Min, parsed.Max, parsed.Text, parsed.Max + 1000));
        }

        if (!candidates.Any())
            return (0, 0, string.Empty, isHandmade);

        var best = candidates.OrderByDescending(x => x.Score).ThenByDescending(x => x.Max).First();
        return (best.Min, best.Max, NormalizeProductionText(best.Original, best.Min, best.Max), true);
    }

    private static IEnumerable<(int Min, int Max, string Text)> ParseDurations(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            yield break;

        foreach (Match m in Regex.Matches(text, @"(?i)(?:about|approx(?:imately)?|around|usually|up to|within)?\s*(\d+)\s*(?:-|to|–|—)?\s*(\d+)?\s*(business\s*)?(day|days|week|weeks|month|months)"))
        {
            var a = int.TryParse(m.Groups[1].Value, out var av) ? av : 0;
            var b = int.TryParse(m.Groups[2].Value, out var bv) ? bv : a;
            var unit = m.Groups[4].Value.ToLowerInvariant();
            var factor = unit.StartsWith("week") ? 7 : unit.StartsWith("month") ? 30 : 1;
            var min = Math.Max(0, Math.Min(a, b) * factor);
            var max = Math.Max(min, Math.Max(a, b) * factor);

            // "about 1 week" is naturally fuzzy; keep a small buffer so the customer sees a realistic window.
            if (m.Value.Contains("about", StringComparison.OrdinalIgnoreCase) && min == max)
                max += factor <= 1 ? 1 : 3;

            yield return (min, max, m.Value.Trim());
        }
    }

    private static string NormalizeProductionText(string original, int minDays, int maxDays)
    {
        if (!string.IsNullOrWhiteSpace(original) && original.Length <= 80)
            return original.Trim();

        return BuildProductionText(minDays, maxDays);
    }

    private static string BuildProductionText(int minDays, int maxDays)
    {
        if (minDays <= 0 && maxDays <= 0)
            return string.Empty;

        if (minDays == maxDays)
        {
            if (maxDays % 7 == 0 && maxDays >= 7)
                return maxDays == 7 ? "About 1 week" : $"About {maxDays / 7} weeks";
            return $"About {maxDays} days";
        }

        if (minDays % 7 == 0 && maxDays % 7 == 0 && maxDays >= 7)
            return $"About {minDays / 7}-{maxDays / 7} weeks";

        return $"About {minDays}-{maxDays} days";
    }
}
