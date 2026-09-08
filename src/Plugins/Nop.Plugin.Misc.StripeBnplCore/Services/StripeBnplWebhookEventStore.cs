using LinqToDB;
using Nop.Data;
using Nop.Plugin.Misc.StripeBnplCore.Domain;

namespace Nop.Plugin.Misc.StripeBnplCore.Services;

public sealed class StripeBnplWebhookEventStore : IStripeBnplWebhookEventStore
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);
    private readonly IRepository<StripeBnplWebhookEvent> _repository;

    public StripeBnplWebhookEventStore(IRepository<StripeBnplWebhookEvent> repository)
    {
        _repository = repository;
    }

    public async Task<StripeBnplWebhookBeginResult> TryBeginAsync(StripeBnplWebhookEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (string.IsNullOrWhiteSpace(envelope.EventId))
            throw new ArgumentException("Stripe event id is required.", nameof(envelope));

        var token = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow;
        var existing = await GetAsync(envelope.EventId);
        if (existing == null)
        {
            var record = new StripeBnplWebhookEvent
            {
                EventId = envelope.EventId,
                EventType = envelope.EventType,
                ObjectId = envelope.ObjectId,
                OrderId = envelope.OrderId,
                ProcessingStatus = "received",
                ProcessingToken = token,
                LeaseExpiresOnUtc = now.Add(LeaseDuration),
                EventCreatedOnUtc = envelope.EventCreatedOnUtc,
                CreatedOnUtc = now,
                ProcessedOnUtc = now
            };

            try
            {
                await _repository.InsertAsync(record, publishEvent: false);
                return new(StripeBnplWebhookBeginState.Started, token);
            }
            catch
            {
                existing = await GetAsync(envelope.EventId);
                if (existing == null)
                    throw;
            }
        }

        if (IsTerminal(existing.ProcessingStatus))
            return new(StripeBnplWebhookBeginState.Duplicate, null);
        if (string.Equals(existing.ProcessingStatus, "received", StringComparison.OrdinalIgnoreCase) &&
            existing.LeaseExpiresOnUtc > now)
            return new(StripeBnplWebhookBeginState.InProgress, null);

        var affected = await _repository.Table
            .Where(item => item.Id == existing.Id &&
                           item.ProcessingStatus == existing.ProcessingStatus &&
                           item.ProcessingToken == existing.ProcessingToken &&
                           item.LeaseExpiresOnUtc == existing.LeaseExpiresOnUtc &&
                           item.ProcessedOnUtc == existing.ProcessedOnUtc)
            .Set(item => item.ProcessingStatus, "received")
            .Set(item => item.ProcessingToken, token)
            .Set(item => item.LeaseExpiresOnUtc, (DateTime?)now.Add(LeaseDuration))
            .Set(item => item.Error, (string)null)
            .Set(item => item.ProcessedOnUtc, now)
            .UpdateAsync();
        return affected == 1
            ? new(StripeBnplWebhookBeginState.Started, token)
            : new(StripeBnplWebhookBeginState.InProgress, null);
    }

    public async Task CompleteAsync(string eventId, string processingToken, string status, int? orderId = null)
    {
        ValidateToken(eventId, processingToken);
        var normalized = NormalizeTerminalStatus(status);
        var now = DateTime.UtcNow;
        var query = _repository.Table.Where(item => item.EventId == eventId &&
            item.ProcessingStatus == "received" && item.ProcessingToken == processingToken);
        var affected = orderId.HasValue
            ? await query
                .Set(item => item.ProcessingStatus, normalized)
                .Set(item => item.OrderId, (int?)orderId.Value)
                .Set(item => item.ProcessingToken, (string)null)
                .Set(item => item.LeaseExpiresOnUtc, (DateTime?)null)
                .Set(item => item.Error, (string)null)
                .Set(item => item.ProcessedOnUtc, now)
                .UpdateAsync()
            : await query
                .Set(item => item.ProcessingStatus, normalized)
                .Set(item => item.ProcessingToken, (string)null)
                .Set(item => item.LeaseExpiresOnUtc, (DateTime?)null)
                .Set(item => item.Error, (string)null)
                .Set(item => item.ProcessedOnUtc, now)
                .UpdateAsync();
        if (affected == 1)
            return;

        var current = await GetAsync(eventId);
        if (current != null && IsTerminal(current.ProcessingStatus))
            return;
        throw new InvalidOperationException("Stripe webhook completion lost its processing lease.");
    }

    public async Task FailAsync(string eventId, string processingToken, Exception exception)
    {
        ValidateToken(eventId, processingToken);
        var message = exception?.Message;
        var safeMessage = string.IsNullOrEmpty(message) || message.Length <= 4000 ? message : message[..4000];
        await _repository.Table
            .Where(item => item.EventId == eventId && item.ProcessingStatus == "received" &&
                           item.ProcessingToken == processingToken)
            .Set(item => item.ProcessingStatus, "failed")
            .Set(item => item.ProcessingToken, (string)null)
            .Set(item => item.LeaseExpiresOnUtc, (DateTime?)null)
            .Set(item => item.Error, safeMessage)
            .Set(item => item.ProcessedOnUtc, DateTime.UtcNow)
            .UpdateAsync();
    }

    internal static string NormalizeTerminalStatus(string status) =>
        string.Equals(status, "ignored", StringComparison.OrdinalIgnoreCase) ? "ignored" : "processed";

    private static bool IsTerminal(string status) =>
        string.Equals(status, "processed", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, "ignored", StringComparison.OrdinalIgnoreCase);

    private static void ValidateToken(string eventId, string processingToken)
    {
        if (string.IsNullOrWhiteSpace(eventId) || string.IsNullOrWhiteSpace(processingToken))
            throw new ArgumentException("Stripe webhook event id and processing token are required.");
    }

    private Task<StripeBnplWebhookEvent> GetAsync(string eventId) =>
        _repository.Table.FirstOrDefaultAsync(item => item.EventId == eventId);
}
