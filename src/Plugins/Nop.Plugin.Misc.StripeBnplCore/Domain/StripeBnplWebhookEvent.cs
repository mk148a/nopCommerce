using Nop.Core;

namespace Nop.Plugin.Misc.StripeBnplCore.Domain;

public class StripeBnplWebhookEvent : BaseEntity
{
    public string EventId { get; set; }
    public string EventType { get; set; }
    public string ObjectId { get; set; }
    public int? OrderId { get; set; }
    public string ProcessingStatus { get; set; }
    public string ProcessingToken { get; set; }
    public DateTime? LeaseExpiresOnUtc { get; set; }
    public string Error { get; set; }
    public DateTime EventCreatedOnUtc { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime ProcessedOnUtc { get; set; }
}
