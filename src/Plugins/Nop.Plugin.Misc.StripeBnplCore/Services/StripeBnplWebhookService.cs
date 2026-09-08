using System.Text.Json;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Misc.StripeBnplCore.Domain;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Stripe;

namespace Nop.Plugin.Misc.StripeBnplCore.Services;

public sealed class StripeBnplWebhookService : IStripeBnplWebhookService
{
    private readonly IOrderService _orderService;
    private readonly IOrderProcessingService _orderProcessingService;
    private readonly IStripeBnplCheckoutSessionStore _sessionStore;
    private readonly IStripeBnplPendingOrderCleanupService _pendingOrderCleanupService;
    private readonly IStripeBnplPaymentFinalizer _finalizer;
    private readonly IStripeBnplPaymentRecordStore _paymentRecordStore;
    private readonly IStripeBnplWebhookEventStore _eventStore;
    private readonly ILogger _logger;
    private readonly StripeBnplSettings _settings;

    public StripeBnplWebhookService(
        IOrderService orderService,
        IOrderProcessingService orderProcessingService,
        IStripeBnplCheckoutSessionStore sessionStore,
        IStripeBnplPendingOrderCleanupService pendingOrderCleanupService,
        IStripeBnplPaymentFinalizer finalizer,
        IStripeBnplPaymentRecordStore paymentRecordStore,
        IStripeBnplWebhookEventStore eventStore,
        ILogger logger,
        StripeBnplSettings settings)
    {
        _orderService = orderService;
        _orderProcessingService = orderProcessingService;
        _sessionStore = sessionStore;
        _pendingOrderCleanupService = pendingOrderCleanupService;
        _finalizer = finalizer;
        _paymentRecordStore = paymentRecordStore;
        _eventStore = eventStore;
        _logger = logger;
        _settings = settings;
    }

    public async Task ProcessAsync(string payload, string signature)
    {
        if (string.IsNullOrWhiteSpace(payload))
            throw new StripeBnplWebhookValidationException("Stripe webhook payload is empty.");
        if (string.IsNullOrWhiteSpace(signature))
            throw new StripeBnplWebhookValidationException("Stripe-Signature header is missing.");
        if (string.IsNullOrWhiteSpace(_settings.GetActiveWebhookSecret()))
            throw new StripeBnplWebhookValidationException("Stripe webhook signing secret is not configured.");

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(payload, signature, _settings.GetActiveWebhookSecret(),
                _settings.WebhookSignatureToleranceSeconds, throwOnApiVersionMismatch: false);
        }
        catch (Exception exception)
        {
            throw new StripeBnplWebhookValidationException("Stripe webhook signature or payload validation failed.", exception);
        }

        if (stripeEvent == null || string.IsNullOrWhiteSpace(stripeEvent.Id))
            throw new StripeBnplWebhookValidationException("Stripe webhook event id is missing.");
        if (_settings.UseSandbox == stripeEvent.Livemode)
            throw new StripeBnplWebhookValidationException("Stripe webhook environment does not match plugin mode.");

        var rawObject = NormalizeRawObject(stripeEvent.Data);
        var objectId = (stripeEvent.Data?.Object as IHasId)?.Id ?? GetString(rawObject, "id");
        var orderId = ParseOrderId(rawObject);
        var begin = await _eventStore.TryBeginAsync(new StripeBnplWebhookEnvelope(
            stripeEvent.Id, stripeEvent.Type, objectId, orderId, stripeEvent.Created));
        if (begin.State == StripeBnplWebhookBeginState.Duplicate)
            return;
        if (begin.State == StripeBnplWebhookBeginState.InProgress)
            throw new StripeBnplWebhookRetryException(
                "Another worker is processing this Stripe webhook event; Stripe must retry it.");

        try
        {
            var (status, resolvedOrderId) = await DispatchAsync(stripeEvent, rawObject, objectId);
            await _eventStore.CompleteAsync(stripeEvent.Id, begin.ProcessingToken, status,
                resolvedOrderId ?? orderId);
        }
        catch (Exception exception)
        {
            await _eventStore.FailAsync(stripeEvent.Id, begin.ProcessingToken, exception);
            await _logger.ErrorAsync($"[Stripe BNPL] Webhook {stripeEvent.Id} ({stripeEvent.Type}) failed.", exception);
            throw;
        }
    }

    private async Task<(string Status, int? OrderId)> DispatchAsync(Event stripeEvent, JsonElement? rawObject,
        string objectId)
    {
        switch (stripeEvent.Type)
        {
            case "checkout.session.completed":
            case "checkout.session.async_payment_succeeded":
            {
                var result = await _finalizer.FinalizeSessionAsync(objectId, source: stripeEvent.Type);
                EnsureFinalizationIsNotInProgress(result);
                return (result.IsPaid ? "processed" : "ignored", result.OrderId);
            }
            case "payment_intent.succeeded":
            {
                var session = await _sessionStore.GetByPaymentIntentIdAsync(objectId);
                if (session == null)
                {
                    if (IsOwnedBnplObject(rawObject))
                        throw new InvalidOperationException(
                            "Stripe BNPL PaymentIntent arrived before its Checkout Session record; retry is required.");
                    return ("ignored", null);
                }
                var result = await _finalizer.FinalizeSessionAsync(session.SessionId, source: stripeEvent.Type);
                EnsureFinalizationIsNotInProgress(result);
                return (result.IsPaid ? "processed" : "ignored", result.OrderId);
            }
            case "checkout.session.async_payment_failed":
            case "checkout.session.expired":
            {
                var session = await _sessionStore.GetBySessionIdAsync(objectId);
                if (session == null)
                    return ("ignored", null);
                if (string.Equals(session.Status, "paid", StringComparison.OrdinalIgnoreCase))
                    return ("ignored", session.OrderId);
                await _sessionStore.UpdateStatusAsync(session,
                    stripeEvent.Type.EndsWith("expired", StringComparison.Ordinal) ? "expired" : "payment_failed",
                    GetString(rawObject, "payment_intent"));
                await AddOrderNoteOnceAsync(session.OrderId,
                    $"Stripe BNPL {stripeEvent.Type} received for Session {objectId}; the order was not marked paid.");
                await _pendingOrderCleanupService.TryCancelAsync(session.OrderId, session.Provider,
                    stripeEvent.Type, "webhook", session);
                return ("processed", session.OrderId);
            }
            case "payment_intent.payment_failed":
            case "payment_intent.canceled":
            {
                var session = await _sessionStore.GetByPaymentIntentIdAsync(objectId);
                if (session == null)
                    return ("ignored", null);
                if (string.Equals(session.Status, "paid", StringComparison.OrdinalIgnoreCase))
                    return ("ignored", session.OrderId);
                await _sessionStore.UpdateStatusAsync(session,
                    stripeEvent.Type.EndsWith("canceled", StringComparison.Ordinal) ? "canceled" : "payment_failed");
                await AddOrderNoteOnceAsync(session.OrderId,
                    $"Stripe BNPL {stripeEvent.Type} received for PaymentIntent {objectId}; the order was not marked paid.");
                await _pendingOrderCleanupService.TryCancelAsync(session.OrderId, session.Provider,
                    stripeEvent.Type, "webhook", session);
                return ("processed", session.OrderId);
            }
            case "charge.refunded":
            {
                var chargeId = objectId;
                var record = await ResolvePaymentRecordAsync(chargeId, rawObject);
                if (record == null)
                {
                    if (await IsDeferredOwnedObjectAsync(rawObject))
                        throw new InvalidOperationException(
                            "Stripe BNPL refund arrived before its payment record; retry is required.");
                    return ("ignored", null);
                }
                var refunded = GetLong(rawObject, "amount_refunded");
                var amount = GetLong(rawObject, "amount");
                var currency = GetString(rawObject, "currency");
                if (amount != record.AmountMinor || refunded < 0 || refunded > amount ||
                    !string.Equals(currency, record.Currency, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Stripe refund amount or currency does not match the recorded payment.");

                var refundClaim = await _paymentRecordStore.TryAcquireRefundAsync(record.OrderId, refunded);
                if (refundClaim == StripeBnplRefundClaim.InProgress)
                    throw new InvalidOperationException("Another worker is applying this Stripe BNPL refund.");
                if (refundClaim == StripeBnplRefundClaim.AlreadyApplied)
                    return ("processed", record.OrderId);

                try
                {
                    var order = await _orderService.GetOrderByIdAsync(record.OrderId)
                        ?? throw new InvalidOperationException("Refunded nopCommerce order was not found.");
                    var isFullRefund = refunded == amount && amount > 0;
                    var targetRefundedPrimary = isFullRefund
                        ? order.OrderTotal
                        : order.CurrencyRate > 0
                            ? decimal.Round(StripeBnplCheckoutService.FromMinorUnits(refunded, currency) /
                                            order.CurrencyRate, 4, MidpointRounding.AwayFromZero)
                            : throw new InvalidOperationException("Order currency rate is invalid.");
                    targetRefundedPrimary = Math.Min(order.OrderTotal, targetRefundedPrimary);
                    var increment = targetRefundedPrimary - order.RefundedAmount;
                    if (increment > 0)
                    {
                        if (isFullRefund && order.RefundedAmount == 0 && _orderProcessingService.CanRefundOffline(order))
                            await _orderProcessingService.RefundOfflineAsync(order);
                        else if (_orderProcessingService.CanPartiallyRefundOffline(order, increment))
                            await _orderProcessingService.PartiallyRefundOfflineAsync(order, increment);
                        else
                            throw new InvalidOperationException("nopCommerce cannot apply the Stripe refund transition safely.");
                    }
                    await _paymentRecordStore.CompleteRefundAsync(record.OrderId, refunded,
                        isFullRefund ? "refunded" : "partially_refunded");
                    await AddOrderNoteOnceAsync(record.OrderId,
                        $"Stripe BNPL refund event recorded for Charge {chargeId}. Processing and FX fees may be non-refundable.");
                }
                catch
                {
                    await _paymentRecordStore.FailRefundAsync(record.OrderId, refunded);
                    throw;
                }
                return ("processed", record.OrderId);
            }
            case "charge.dispute.created":
            case "charge.dispute.closed":
            {
                var chargeId = GetString(rawObject, "charge");
                var record = await ResolvePaymentRecordAsync(chargeId, rawObject);
                if (record == null)
                {
                    if (await IsDeferredOwnedObjectAsync(rawObject))
                        throw new InvalidOperationException(
                            "Stripe BNPL dispute arrived before its payment record; retry is required.");
                    return ("ignored", null);
                }
                var disputeStatus = GetString(rawObject, "status") ?? "unknown";
                await _paymentRecordStore.UpdateStatusAsync(record,
                    stripeEvent.Type.EndsWith("created", StringComparison.Ordinal)
                        ? $"disputed_{disputeStatus}"
                        : $"dispute_closed_{disputeStatus}");
                await AddOrderNoteOnceAsync(record.OrderId,
                    $"Stripe BNPL dispute event {stripeEvent.Type} ({objectId}) recorded with status {disputeStatus}.");
                return ("processed", record.OrderId);
            }
            default:
                return ("ignored", null);
        }
    }

    private async Task AddOrderNoteOnceAsync(int orderId, string note)
    {
        var order = await _orderService.GetOrderByIdAsync(orderId);
        if (order == null)
            return;
        var notes = await _orderService.GetOrderNotesByOrderIdAsync(orderId);
        if (notes.Any(item => string.Equals(item.Note, note, StringComparison.Ordinal)))
            return;
        await _orderService.InsertOrderNoteAsync(new OrderNote
        {
            OrderId = orderId,
            Note = note,
            DisplayToCustomer = false,
            CreatedOnUtc = DateTime.UtcNow
        });
    }

    private async Task<StripeBnplPaymentRecord> ResolvePaymentRecordAsync(string chargeId, JsonElement? rawObject)
    {
        var record = await _paymentRecordStore.GetByChargeIdAsync(chargeId);
        if (record != null)
            return record;
        return await _paymentRecordStore.GetByPaymentIntentIdAsync(GetString(rawObject, "payment_intent"));
    }

    private async Task<bool> IsDeferredOwnedObjectAsync(JsonElement? rawObject)
    {
        if (IsOwnedBnplObject(rawObject))
            return true;
        var paymentIntentId = GetString(rawObject, "payment_intent");
        return !string.IsNullOrWhiteSpace(paymentIntentId) &&
               await _sessionStore.GetByPaymentIntentIdAsync(paymentIntentId) != null;
    }

    private static bool IsOwnedBnplObject(JsonElement? rawObject)
    {
        if (!rawObject.HasValue || rawObject.Value.ValueKind != JsonValueKind.Object ||
            !rawObject.Value.TryGetProperty("metadata", out var metadata) ||
            metadata.ValueKind != JsonValueKind.Object)
            return false;
        var provider = metadata.TryGetProperty("provider", out var providerValue) ? providerValue.ToString() : null;
        var paymentSystem = metadata.TryGetProperty("payment_system_name", out var systemValue)
            ? systemValue.ToString()
            : null;
        return StripeBnplDefaults.TryParseProviderSlug(provider, out _) ||
               Enum.GetValues<BnplProvider>().Any(item => string.Equals(paymentSystem,
                   StripeBnplDefaults.GetSystemName(item), StringComparison.OrdinalIgnoreCase));
    }

    private static int? ParseOrderId(JsonElement? element)
    {
        if (!element.HasValue || element.Value.ValueKind != JsonValueKind.Object ||
            !element.Value.TryGetProperty("metadata", out var metadata) ||
            metadata.ValueKind != JsonValueKind.Object ||
            !metadata.TryGetProperty("order_id", out var value))
            return null;
        return int.TryParse(value.ToString(), out var orderId) && orderId > 0 ? orderId : null;
    }

    private static JsonElement? NormalizeRawObject(EventData data)
    {
        static JsonElement? Unwrap(JsonElement? candidate)
        {
            if (!candidate.HasValue || candidate.Value.ValueKind != JsonValueKind.Object)
                return candidate;
            var value = candidate.Value;
            if (value.TryGetProperty("object", out var objectElement) && objectElement.ValueKind == JsonValueKind.Object)
                return objectElement.Clone();
            if (value.TryGetProperty("data", out var dataElement) && dataElement.ValueKind == JsonValueKind.Object &&
                dataElement.TryGetProperty("object", out objectElement) && objectElement.ValueKind == JsonValueKind.Object)
                return objectElement.Clone();
            return value.Clone();
        }

        if (data?.Object is StripeEntity entity && entity.RawJsonElement.HasValue)
            return Unwrap(entity.RawJsonElement);

        var raw = Unwrap(data?.RawJsonElement);
        if (raw.HasValue && GetString(raw, "id") != null)
            return raw;

        if (data?.Object != null)
        {
            return JsonSerializer.SerializeToElement(data.Object, data.Object.GetType(), new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });
        }

        return raw;
    }

    private static string GetString(JsonElement? element, string propertyName)
    {
        if (!element.HasValue || element.Value.ValueKind != JsonValueKind.Object ||
            !element.Value.TryGetProperty(propertyName, out var value))
            return null;
        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }

    private static long GetLong(JsonElement? element, string propertyName)
    {
        var value = GetString(element, propertyName);
        return long.TryParse(value, out var result) ? result : 0;
    }

    private static void EnsureFinalizationIsNotInProgress(StripeBnplFinalizationResult result)
    {
        if (result != null && string.Equals(result.Status, "processing", StringComparison.OrdinalIgnoreCase))
            throw new StripeBnplWebhookRetryException(
                "Another worker is finalizing this Stripe BNPL payment; Stripe must retry the webhook.");
    }
}
