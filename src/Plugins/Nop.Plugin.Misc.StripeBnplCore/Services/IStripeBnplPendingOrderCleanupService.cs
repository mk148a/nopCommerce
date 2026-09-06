using Nop.Plugin.Misc.StripeBnplCore.Domain;

namespace Nop.Plugin.Misc.StripeBnplCore.Services;

public interface IStripeBnplPendingOrderCleanupService
{
    Task<StripeBnplPendingOrderCleanupResult> SweepAsync(int maxOrders = 100);

    Task<bool> TryCancelAsync(int orderId, BnplProvider provider, string reason,
        string source, StripeBnplCheckoutSession knownSession = null);
}

public sealed record StripeBnplPendingOrderCleanupResult(
    int Candidates,
    int Cancelled,
    int Skipped,
    int Failed);
