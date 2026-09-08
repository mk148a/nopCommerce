using Nop.Web.Framework.Models;

namespace Nop.Plugin.Misc.StripeBnplCore.Areas.Admin.Models;

public record StripeBnplCostReportModel : BaseNopModel
{
    public int? OrderId { get; set; }
    public int? ProviderId { get; set; }
    public bool? IsSandbox { get; set; }
    public string FeeDataStatus { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public IReadOnlyList<StripeBnplCostReportRowModel> Rows { get; set; } =
        Array.Empty<StripeBnplCostReportRowModel>();
}

public record StripeBnplCostReportRowModel : BaseNopEntityModel
{
    public int OrderId { get; set; }
    public string OrderNumber { get; set; }
    public string Provider { get; set; }
    public string Environment { get; set; }
    public string PaymentMethodType { get; set; }
    public string PaymentStatus { get; set; }
    public string Amount { get; set; }
    public string RefundedAmount { get; set; }
    public string Fee { get; set; }
    public string Net { get; set; }
    public string ExchangeRate { get; set; }
    public string FeeDataStatus { get; set; }
    public string FeeDataError { get; set; }
    public int FeeReconciliationAttemptCount { get; set; }
    public DateTime? FeeLastAttemptOnUtc { get; set; }
    public DateTime? FeeCompletedOnUtc { get; set; }
    public string PaymentIntentId { get; set; }
    public string BalanceTransactionId { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public IReadOnlyList<StripeBnplFeeDetailModel> FeeDetails { get; set; } =
        Array.Empty<StripeBnplFeeDetailModel>();
}

public record StripeBnplFeeDetailModel : BaseNopModel
{
    public string Type { get; set; }
    public string Description { get; set; }
    public string Amount { get; set; }
}
