namespace Nop.Plugin.Misc.StripeBnplCore.Services;

public interface IStripeBnplPaymentFinalizer
{
    Task<StripeBnplFinalizationResult> FinalizeSessionAsync(string sessionId, Guid? expectedOrderGuid = null,
        string source = "webhook");
}

public sealed record StripeBnplFinalizationResult(
    int OrderId,
    bool IsPaid,
    bool AlreadyPaid,
    string Status,
    string Reason);
