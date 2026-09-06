using Nop.Plugin.Misc.StripeBnplCore.Domain;

namespace Nop.Plugin.Misc.StripeBnplCore.Services;

public enum StripeBnplWebhookBeginState
{
    Started,
    Duplicate,
    InProgress
}

public sealed record StripeBnplWebhookEnvelope(
    string EventId,
    string EventType,
    string ObjectId,
    int? OrderId,
    DateTime EventCreatedOnUtc);

public sealed record StripeBnplWebhookBeginResult(StripeBnplWebhookBeginState State, string ProcessingToken);

public interface IStripeBnplWebhookEventStore
{
    Task<StripeBnplWebhookBeginResult> TryBeginAsync(StripeBnplWebhookEnvelope envelope);
    Task CompleteAsync(string eventId, string processingToken, string status, int? orderId = null);
    Task FailAsync(string eventId, string processingToken, Exception exception);
}
