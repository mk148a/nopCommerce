namespace Nop.Plugin.Misc.StripeBnplCore.Services;

public sealed record BnplCartEligibilityResult(
    bool IsEligible,
    string ReasonCode,
    string Reason,
    IReadOnlyCollection<int> BlockingProductIds)
{
    public static BnplCartEligibilityResult Eligible() =>
        new(true, "eligible", "All products are explicitly approved for this provider.", Array.Empty<int>());

    public static BnplCartEligibilityResult Blocked(string code, string reason, IEnumerable<int> productIds = null) =>
        new(false, code, reason, (productIds ?? Array.Empty<int>()).Distinct().ToArray());
}
