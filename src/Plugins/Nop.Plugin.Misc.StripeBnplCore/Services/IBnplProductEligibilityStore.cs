using Nop.Plugin.Misc.StripeBnplCore.Domain;

namespace Nop.Plugin.Misc.StripeBnplCore.Services;

public interface IBnplProductEligibilityStore
{
    Task<IReadOnlyCollection<BnplProductEligibility>> GetByProductIdsAsync(
        IReadOnlyCollection<int> productIds, BnplProvider provider);

    Task<BnplProductEligibility> GetAsync(int productId, BnplProvider provider);

    Task<BnplProductEligibility> UpsertAsync(int productId, BnplProvider provider,
        BnplEligibilityState state, string reason, int? fulfillmentDays,
        string approvalReference, string policyVersion);
}
