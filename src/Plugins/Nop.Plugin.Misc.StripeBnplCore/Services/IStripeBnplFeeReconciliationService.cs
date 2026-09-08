namespace Nop.Plugin.Misc.StripeBnplCore.Services;

public sealed record StripeBnplFeeReconciliationResult(int Candidates, int Completed, int Failed, int Skipped);

public interface IStripeBnplFeeReconciliationService
{
    Task<StripeBnplFeeReconciliationResult> ReconcileAsync(int maxRecords = 50);
}
