using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.Misc.StripeBnplCore.Areas.Admin.Models;
using Nop.Plugin.Misc.StripeBnplCore.Domain;
using Nop.Plugin.Misc.StripeBnplCore.Services;
using Nop.Services.Orders;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Misc.StripeBnplCore.Areas.Admin.Controllers;

[Area(AreaNames.ADMIN)]
[AuthorizeAdmin]
[AutoValidateAntiforgeryToken]
public sealed class StripeBnplCostReportController : BasePluginController
{
    internal const string CostReportViewPath =
        "~/Plugins/Misc.StripeBnplCore/Areas/Admin/Views/StripeBnplCostReport/Index.cshtml";

    private readonly IOrderService _orderService;
    private readonly IStripeBnplPaymentRecordStore _paymentRecordStore;

    public StripeBnplCostReportController(IOrderService orderService,
        IStripeBnplPaymentRecordStore paymentRecordStore)
    {
        _orderService = orderService;
        _paymentRecordStore = paymentRecordStore;
    }

    [CheckPermission(StandardPermission.Configuration.MANAGE_PAYMENT_METHODS)]
    public async Task<IActionResult> Index(StripeBnplCostReportModel search)
    {
        search ??= new StripeBnplCostReportModel();
        var pageSize = Math.Clamp(search.PageSize, 10, 100);
        var pageNumber = Math.Max(1, search.PageNumber);
        var orderId = search.OrderId is > 0 ? search.OrderId : null;
        var provider = TryGetProvider(search.ProviderId, out var parsedProvider) ? parsedProvider : null;
        var feeDataStatus = NormalizeFeeDataStatus(search.FeeDataStatus);

        var page = await _paymentRecordStore.SearchCostReportAsync(new StripeBnplCostReportQuery(
            orderId, provider, search.IsSandbox, feeDataStatus, pageNumber - 1, pageSize));
        var totalPages = Math.Max(1, (int)Math.Ceiling(page.TotalCount / (decimal)pageSize));

        // A concurrent insert/delete can move the requested page. Re-read the
        // last valid page so the report never renders an impossible page index.
        if (pageNumber > totalPages)
        {
            pageNumber = totalPages;
            page = await _paymentRecordStore.SearchCostReportAsync(new StripeBnplCostReportQuery(
                orderId, provider, search.IsSandbox, feeDataStatus, pageNumber - 1, pageSize));
        }

        var orders = (await _orderService.GetOrdersByIdsAsync(page.Records
                .Select(record => record.OrderId).Distinct().ToArray()))
            .ToDictionary(order => order.Id);
        var rows = page.Records.Select(record =>
        {
            orders.TryGetValue(record.OrderId, out var order);
            return CreateRowModel(record, order?.CustomOrderNumber);
        }).ToArray();

        return View(CostReportViewPath, new StripeBnplCostReportModel
        {
            OrderId = orderId,
            ProviderId = provider.HasValue ? (int)provider.Value : null,
            IsSandbox = search.IsSandbox,
            FeeDataStatus = feeDataStatus,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = page.TotalCount,
            TotalPages = totalPages,
            Rows = rows
        });
    }

    internal static StripeBnplCostReportRowModel CreateRowModel(StripeBnplPaymentRecord record,
        string orderNumber)
    {
        ArgumentNullException.ThrowIfNull(record);
        var provider = Enum.IsDefined(record.Provider) ? record.Provider.ToString() : $"Unknown ({record.ProviderId})";
        return new StripeBnplCostReportRowModel
        {
            Id = record.Id,
            OrderId = record.OrderId,
            OrderNumber = string.IsNullOrWhiteSpace(orderNumber) ? record.OrderId.ToString(CultureInfo.InvariantCulture) : orderNumber,
            Provider = provider,
            Environment = record.IsSandbox ? "Test" : "Live",
            PaymentMethodType = SafeText(record.PaymentMethodType, 64, "Not recorded"),
            PaymentStatus = SafeText(record.Status, 64, "Unknown"),
            Amount = FormatMinor(record.AmountMinor, record.Currency),
            RefundedAmount = FormatMinor(record.RefundedAmountMinor, record.Currency),
            Fee = FormatMinor(record.FeeMinor, record.SettlementCurrency),
            Net = FormatMinor(record.NetMinor, record.SettlementCurrency),
            ExchangeRate = record.ExchangeRate?.ToString("0.########", CultureInfo.InvariantCulture) ?? "—",
            FeeDataStatus = SafeText(record.FeeDataStatus, 32, StripeBnplFeeDataStatus.Pending),
            FeeDataError = SafeText(record.FeeDataError, 1000, null),
            FeeReconciliationAttemptCount = record.FeeReconciliationAttemptCount,
            FeeLastAttemptOnUtc = record.FeeLastAttemptOnUtc,
            FeeCompletedOnUtc = record.FeeCompletedOnUtc,
            PaymentIntentId = SafeText(record.PaymentIntentId, 255, null),
            BalanceTransactionId = SafeText(record.BalanceTransactionId, 255, null),
            CreatedOnUtc = record.CreatedOnUtc,
            FeeDetails = ParseFeeDetails(record.FeeDetailsJson)
        };
    }

    internal static string FormatMinor(long? amountMinor, string currency)
    {
        if (!amountMinor.HasValue || string.IsNullOrWhiteSpace(currency))
            return "—";

        var value = StripeBnplCheckoutService.FromMinorUnits(amountMinor.Value, currency);
        return $"{value.ToString("0.###", CultureInfo.InvariantCulture)} {currency.Trim().ToUpperInvariant()}";
    }

    internal static IReadOnlyList<StripeBnplFeeDetailModel> ParseFeeDetails(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<StripeBnplFeeDetailModel>();

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return Array.Empty<StripeBnplFeeDetailModel>();

            var details = new List<StripeBnplFeeDetailModel>();
            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object)
                    continue;
                var amount = GetInt64(element, "Amount");
                var currency = GetString(element, "Currency");
                details.Add(new StripeBnplFeeDetailModel
                {
                    Type = SafeText(GetString(element, "Type"), 64, "fee"),
                    Description = SafeText(GetString(element, "Description"), 300, null),
                    Amount = FormatMinor(amount, currency)
                });
            }

            return details;
        }
        catch (JsonException)
        {
            // Never render raw or malformed provider data in the administrator UI.
            return Array.Empty<StripeBnplFeeDetailModel>();
        }
    }

    private static bool TryGetProvider(int? providerId, out BnplProvider? provider)
    {
        provider = null;
        if (!providerId.HasValue || !Enum.IsDefined(typeof(BnplProvider), providerId.Value))
            return false;
        provider = (BnplProvider)providerId.Value;
        return true;
    }

    private static string NormalizeFeeDataStatus(string value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return normalized is StripeBnplFeeDataStatus.Pending or StripeBnplFeeDataStatus.Reconciling or
            StripeBnplFeeDataStatus.Complete or StripeBnplFeeDataStatus.Error
            ? normalized
            : null;
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        foreach (var property in element.EnumerateObject())
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase) &&
                property.Value.ValueKind == JsonValueKind.String)
                return property.Value.GetString();
        return null;
    }

    private static long? GetInt64(JsonElement element, string propertyName)
    {
        foreach (var property in element.EnumerateObject())
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase) &&
                property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetInt64(out var value))
                return value;
        return null;
    }

    private static string SafeText(string value, int maxLength, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;
        value = value.Trim();
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
