using Nop.Core;

namespace Nop.Plugin.Misc.StripeBnplCore.Domain;

public class BnplProductEligibility : BaseEntity
{
    public int ProductId { get; set; }
    public int ProviderId { get; set; }
    public int EligibilityStateId { get; set; }
    public string Reason { get; set; }
    public int? FulfillmentDays { get; set; }
    public string ApprovalReference { get; set; }
    public string PolicyVersion { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime UpdatedOnUtc { get; set; }

    public BnplProvider Provider
    {
        get => (BnplProvider)ProviderId;
        set => ProviderId = (int)value;
    }

    public BnplEligibilityState EligibilityState
    {
        get => (BnplEligibilityState)EligibilityStateId;
        set => EligibilityStateId = (int)value;
    }
}
