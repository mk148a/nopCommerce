using LinqToDB;
using Nop.Data;
using Nop.Plugin.Misc.StripeBnplCore.Domain;

namespace Nop.Plugin.Misc.StripeBnplCore.Services;

public sealed class BnplProductEligibilityStore : IBnplProductEligibilityStore
{
    private readonly IRepository<BnplProductEligibility> _repository;

    public BnplProductEligibilityStore(IRepository<BnplProductEligibility> repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyCollection<BnplProductEligibility>> GetByProductIdsAsync(
        IReadOnlyCollection<int> productIds, BnplProvider provider)
    {
        if (productIds == null || productIds.Count == 0)
            return Array.Empty<BnplProductEligibility>();

        var ids = productIds.Distinct().ToArray();
        return await _repository.Table
            .Where(item => ids.Contains(item.ProductId) && item.ProviderId == (int)provider)
            .ToListAsync();
    }

    public Task<BnplProductEligibility> GetAsync(int productId, BnplProvider provider) =>
        _repository.Table.FirstOrDefaultAsync(item =>
            item.ProductId == productId && item.ProviderId == (int)provider);

    public async Task<BnplProductEligibility> UpsertAsync(int productId, BnplProvider provider,
        BnplEligibilityState state, string reason, int? fulfillmentDays,
        string approvalReference, string policyVersion)
    {
        var now = DateTime.UtcNow;
        var record = await GetAsync(productId, provider);
        if (record == null)
        {
            record = new BnplProductEligibility
            {
                ProductId = productId,
                Provider = provider,
                CreatedOnUtc = now
            };
        }

        record.EligibilityState = state;
        record.Reason = Normalize(reason, 2_000);
        record.FulfillmentDays = fulfillmentDays;
        record.ApprovalReference = Normalize(approvalReference, 500);
        record.PolicyVersion = Normalize(policyVersion, 500);
        record.UpdatedOnUtc = now;

        if (record.Id == 0)
            await _repository.InsertAsync(record);
        else
            await _repository.UpdateAsync(record);

        return record;
    }

    private static string Normalize(string value, int maximumLength)
    {
        value = value?.Trim();
        if (string.IsNullOrEmpty(value))
            return null;
        return value.Length <= maximumLength ? value : value[..maximumLength];
    }
}
