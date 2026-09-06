using Nop.Web.Framework.Models;

namespace Nop.Plugin.Misc.StripeBnplCore.Areas.Admin.Models;

public record BnplEligibilityMatrixModel : BaseNopModel
{
    public string SearchProductName { get; set; }
    public int ProviderId { get; set; } = 1;
    public int? EligibilityStateId { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public int CategoryId { get; set; }
    public IReadOnlyList<BnplCategoryOptionModel> Categories { get; set; } = Array.Empty<BnplCategoryOptionModel>();
    public IReadOnlyList<BnplEligibilityRowModel> Rows { get; set; } = Array.Empty<BnplEligibilityRowModel>();
}

public record BnplCategoryOptionModel : BaseNopEntityModel
{
    public string Name { get; set; }
}

public record BnplEligibilityRowModel : BaseNopEntityModel
{
    public int ProductId { get; set; }
    public string ProductName { get; set; }
    public string Sku { get; set; }
    public int ProviderId { get; set; }
    public int EligibilityStateId { get; set; }
    public string Reason { get; set; }
    public int? FulfillmentDays { get; set; }
    public string ApprovalReference { get; set; }
    public string PolicyVersion { get; set; }
    public string RestrictedTerm { get; set; }
    public int ExistingEligibilityStateId { get; set; }
}

public record BnplEligibilityUpdateModel : BaseNopModel
{
    public int ProductId { get; set; }
    public int ProviderId { get; set; }
    public int EligibilityStateId { get; set; }
    public string Reason { get; set; }
    public int? FulfillmentDays { get; set; }
    public string ApprovalReference { get; set; }
    public string PolicyVersion { get; set; }
    public string SearchProductName { get; set; }
    public int? EligibilityStateFilterId { get; set; }
    public int CategoryId { get; set; }
    public int PageSize { get; set; } = 25;
    public int PageNumber { get; set; } = 1;
}

public record BnplEligibilityBulkUpdateModel : BaseNopModel
{
    public int CategoryId { get; set; }
    public int ProviderId { get; set; }
    public int EligibilityStateId { get; set; }
    public string Reason { get; set; }
    public int? FulfillmentDays { get; set; }
    public string ApprovalReference { get; set; }
    public string PolicyVersion { get; set; }
}
