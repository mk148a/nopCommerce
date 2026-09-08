using Nop.Plugin.Misc.StripeBnplCore.Domain;

namespace Nop.Plugin.Misc.StripeBnplCore.Services;

public interface IStripeBnplConfigurationClient
{
    Task<StripeBnplConfigurationHealth> CheckProviderOnlyAsync(BnplProvider provider, string configurationId,
        bool expectedLivemode);
    Task EnsureProviderOnlyAsync(BnplProvider provider, string configurationId, bool expectedLivemode);
}

public sealed record StripeBnplConfigurationHealth(bool IsHealthy, string Reason);
