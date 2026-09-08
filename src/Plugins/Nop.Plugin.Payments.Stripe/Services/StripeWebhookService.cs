using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using LinqToDB;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Data;
using Nop.Plugin.Payments.Stripe.Domains;
using Nop.Services.Configuration;
using Nop.Services.Common;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Stripe;

namespace Nop.Plugin.Payments.Stripe.Services;

/// <summary>
/// Handles signed Stripe webhooks and the explicit admin endpoint synchronization action.
/// </summary>
public sealed class StripeWebhookService : IStripeWebhookService
{
    private const int SignatureToleranceSeconds = 300;

    private readonly IRepository<Order> _orderRepository;
    private readonly IRepository<StripeWebhookEvent> _eventRepository;
    private readonly IOrderService _orderService;
    private readonly IGenericAttributeService _genericAttributeService;
    private readonly ISettingService _settingService;
    private readonly ILogger _logger;
    private readonly IWebHelper _webHelper;
    private readonly StripePaymentSettings _settings;

    public StripeWebhookService(IRepository<Order> orderRepository,
        IRepository<StripeWebhookEvent> eventRepository,
        IOrderService orderService,
        IGenericAttributeService genericAttributeService,
        ISettingService settingService,
        ILogger logger,
        IWebHelper webHelper,
        StripePaymentSettings settings)
    {
        _orderRepository = orderRepository;
        _eventRepository = eventRepository;
        _orderService = orderService;
        _genericAttributeService = genericAttributeService;
        _settingService = settingService;
        _logger = logger;
        _webHelper = webHelper;
        _settings = settings;
    }

    public async Task ProcessAsync(string payload, string signature)
    {
        if (string.IsNullOrWhiteSpace(payload))
            throw new StripeWebhookValidationException("Stripe webhook payload is empty.");

        if (string.IsNullOrWhiteSpace(signature))
            throw new StripeWebhookValidationException("Stripe-Signature header is missing.");

        if (string.IsNullOrWhiteSpace(_settings.GetActiveWebhookSecret()))
            throw new StripeWebhookValidationException("Stripe webhook signing secret is not configured.");

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(payload, signature, _settings.GetActiveWebhookSecret(),
                SignatureToleranceSeconds, throwOnApiVersionMismatch: false);
        }
        catch (Exception exception)
        {
            throw new StripeWebhookValidationException("Stripe webhook signature or payload validation failed.", exception);
        }

        if (stripeEvent == null || string.IsNullOrWhiteSpace(stripeEvent.Id))
            throw new StripeWebhookValidationException("Stripe webhook event identifier is missing.");

        var existingEvent = await _eventRepository.Table
            .FirstOrDefaultAsync(item => item.EventId == stripeEvent.Id);

        if (existingEvent != null &&
            string.Equals(existingEvent.ProcessingStatus, "processed", StringComparison.OrdinalIgnoreCase))
        {
            await _logger.InformationAsync($"[Stripe] Ignored duplicate webhook {stripeEvent.Id} ({stripeEvent.Type}).");
            return;
        }

        var paymentIntent = stripeEvent.Data?.Object as PaymentIntent;
        var rawObject = stripeEvent.Data?.RawJsonElement;
        var paymentIntentId = paymentIntent?.Id ?? GetStringProperty(rawObject, "id");
        var paymentIntentStatus = paymentIntent?.Status ?? GetStringProperty(rawObject, "status");
        var orderId = ParseOrderId(paymentIntent?.Metadata, rawObject);

        if (existingEvent == null)
        {
            existingEvent = new StripeWebhookEvent
            {
                EventId = stripeEvent.Id,
                EventType = stripeEvent.Type,
                PaymentIntentId = paymentIntentId,
                OrderId = orderId,
                ProcessedOnUtc = DateTime.UtcNow,
                ProcessingStatus = "received"
            };

            try
            {
                await _eventRepository.InsertAsync(existingEvent, publishEvent: false);
            }
            catch (Exception exception)
            {
                // The unique event index makes concurrent Stripe retries safe. If another
                // request inserted the event first, treat this request as the duplicate.
                var concurrentEvent = await _eventRepository.Table
                    .FirstOrDefaultAsync(item => item.EventId == stripeEvent.Id);
                if (concurrentEvent != null)
                {
                    if (string.Equals(concurrentEvent.ProcessingStatus, "processed", StringComparison.OrdinalIgnoreCase))
                        return;

                    existingEvent = concurrentEvent;
                }
                else
                {
                    await _logger.ErrorAsync($"[Stripe] Could not record webhook {stripeEvent.Id}.", exception);
                    throw;
                }
            }
        }

        try
        {
            var order = await FindOrderAsync(paymentIntentId, orderId);
            var status = await ApplyEventAsync(stripeEvent.Type, paymentIntentStatus, paymentIntentId, order);

            existingEvent.EventType = stripeEvent.Type;
            existingEvent.PaymentIntentId = paymentIntentId;
            existingEvent.OrderId = order?.Id ?? orderId;
            existingEvent.ProcessedOnUtc = DateTime.UtcNow;
            existingEvent.ProcessingStatus = status;
            existingEvent.Error = null;
            await _eventRepository.UpdateAsync(existingEvent, publishEvent: false);
        }
        catch (Exception exception)
        {
            existingEvent.ProcessedOnUtc = DateTime.UtcNow;
            existingEvent.ProcessingStatus = "failed";
            existingEvent.Error = exception.Message.Length > 4000
                ? exception.Message[..4000]
                : exception.Message;
            await _eventRepository.UpdateAsync(existingEvent, publishEvent: false);
            await _logger.ErrorAsync($"[Stripe] Webhook {stripeEvent.Id} ({stripeEvent.Type}) failed.", exception);
            throw;
        }
    }

    public async Task<StripeWebhookSyncResult> SyncEndpointAsync()
    {
        if (string.IsNullOrWhiteSpace(_settings.GetActiveSecretKey()))
            throw new NopException("Stripe API secret key is not configured.");

        var endpointUrl = BuildEndpointUrl();
        var requestOptions = new RequestOptions
        {
            ApiKey = _settings.GetActiveSecretKey()
        };
        var endpointService = new WebhookEndpointService();
        var endpoints = await endpointService.ListAsync(new WebhookEndpointListOptions { Limit = 100 }, requestOptions);
        var matchingEndpoints = endpoints.Data
            .Where(endpoint => string.Equals(endpoint.Url?.TrimEnd('/'), endpointUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
            .ToList();

        var endpoint = matchingEndpoints
            .FirstOrDefault(item => string.Equals(item.Id, _settings.GetActiveWebhookEndpointId(), StringComparison.OrdinalIgnoreCase))
            ?? matchingEndpoints.FirstOrDefault();

        var created = false;
        var updated = false;
        var signingSecretSaved = false;

        if (endpoint == null && !string.IsNullOrWhiteSpace(_settings.GetActiveWebhookEndpointId()))
        {
            endpoint = endpoints.Data.FirstOrDefault(item =>
                string.Equals(item.Id, _settings.GetActiveWebhookEndpointId(), StringComparison.OrdinalIgnoreCase));
        }

        if (endpoint == null)
        {
            var createOptions = new WebhookEndpointCreateOptions
            {
                Url = endpointUrl,
                Description = "Hood Archery Shop nopCommerce Stripe",
                EnabledEvents = StripePaymentDefaults.WebhookEvents.ToList(),
                Metadata = new Dictionary<string, string>
                {
                    ["nop_plugin"] = StripePaymentDefaults.SystemName,
                    ["managed_by"] = "nopCommerce"
                }
            };

            var idempotencyKey = BuildCreateIdempotencyKey(endpointUrl);
            endpoint = await endpointService.CreateAsync(createOptions,
                new RequestOptions
                {
                    ApiKey = _settings.GetActiveSecretKey(),
                    IdempotencyKey = idempotencyKey
                });
            created = true;

            if (!string.IsNullOrWhiteSpace(endpoint.Secret))
            {
                signingSecretSaved = true;
            }
        }
        else
        {
            var updateOptions = new WebhookEndpointUpdateOptions
            {
                Url = endpointUrl,
                Description = "Hood Archery Shop nopCommerce Stripe",
                EnabledEvents = StripePaymentDefaults.WebhookEvents.ToList(),
                Disabled = false,
                Metadata = new Dictionary<string, string>
                {
                    ["nop_plugin"] = StripePaymentDefaults.SystemName,
                    ["managed_by"] = "nopCommerce"
                }
            };

            endpoint = await endpointService.UpdateAsync(endpoint.Id, updateOptions, requestOptions);
            updated = true;
        }

        _settings.SetActiveWebhookEndpoint(endpoint.Id, endpoint.Url ?? endpointUrl, endpoint.Secret);
        await _settingService.SaveSettingAsync(_settings);
        await _settingService.ClearCacheAsync();

        return new StripeWebhookSyncResult
        {
            EndpointId = endpoint.Id,
            EndpointUrl = _settings.GetActiveWebhookEndpointUrl(),
            EndpointStatus = endpoint.Status,
            Created = created,
            Updated = updated,
            SigningSecretSaved = signingSecretSaved,
            SigningSecretRequired = string.IsNullOrWhiteSpace(_settings.GetActiveWebhookSecret()),
            MatchingEndpointCount = matchingEndpoints.Count
        };
    }

    private async Task<string> ApplyEventAsync(string eventType, string paymentIntentStatus,
        string paymentIntentId, Order order)
    {
        if (order == null)
        {
            await _logger.InformationAsync($"[Stripe] Webhook {eventType} acknowledged; no matching nopCommerce order was found for PaymentIntent {paymentIntentId ?? "(none)"}.");
            return "ignored";
        }

        switch (eventType)
        {
            case "payment_intent.succeeded":
                if (order.PaymentStatus == PaymentStatus.Paid)
                {
                    await MarkWebhookPaymentVerifiedAsync(order, paymentIntentId);
                    return "processed";
                }

                if (order.OrderStatus == OrderStatus.Cancelled ||
                    order.PaymentStatus == PaymentStatus.Voided ||
                    order.PaymentStatus == PaymentStatus.Refunded)
                {
                    await _logger.WarningAsync($"[Stripe] PaymentIntent {paymentIntentId} succeeded after order {order.CustomOrderNumber} became terminal; order was not reopened automatically.");
                    return "ignored";
                }

                order.PaymentStatus = PaymentStatus.Paid;
                order.OrderStatus = order.OrderStatus == OrderStatus.Pending
                    ? OrderStatus.Processing
                    : order.OrderStatus;
                order.PaidDateUtc ??= DateTime.UtcNow;
                await _orderService.UpdateOrderAsync(order);
                await MarkWebhookPaymentVerifiedAsync(order, paymentIntentId);
                await InsertOrderNoteAsync(order, $"Stripe webhook payment_intent.succeeded processed ({paymentIntentId}).");
                await _logger.InformationAsync($"[Stripe] Order {order.CustomOrderNumber} marked paid from webhook {paymentIntentId}.");
                return "processed";

            case "payment_intent.canceled":
                if (order.PaymentStatus == PaymentStatus.Pending && order.OrderStatus != OrderStatus.Cancelled)
                {
                    order.PaymentStatus = PaymentStatus.Voided;
                    order.OrderStatus = OrderStatus.Cancelled;
                    await _orderService.UpdateOrderAsync(order);
                    await InsertOrderNoteAsync(order, $"Stripe webhook payment_intent.canceled processed ({paymentIntentId}).");
                }

                return "processed";

            case "payment_intent.payment_failed":
            case "payment_intent.processing":
            case "payment_intent.requires_action":
                await _logger.InformationAsync($"[Stripe] Webhook {eventType} acknowledged for order {order.CustomOrderNumber}; payment status remains {paymentIntentStatus ?? "unknown"}.");
                return "processed";

            default:
                await _logger.InformationAsync($"[Stripe] Ignored valid, unsupported webhook {eventType} ({paymentIntentId ?? "none"}).");
                return "ignored";
        }
    }

    private Task MarkWebhookPaymentVerifiedAsync(Order order, string paymentIntentId)
    {
        return _genericAttributeService.SaveAttributeAsync(order,
            StripePaymentDefaults.WebhookPaymentVerifiedAttribute,
            paymentIntentId ?? string.Empty, order.StoreId);
    }

    private async Task<Order> FindOrderAsync(string paymentIntentId, int? orderId)
    {
        Order order = null;
        if (!string.IsNullOrWhiteSpace(paymentIntentId))
        {
            order = await _orderRepository.Table.FirstOrDefaultAsync(item =>
                !item.Deleted && item.AuthorizationTransactionId == paymentIntentId &&
                item.PaymentMethodSystemName == StripePaymentDefaults.SystemName);
        }

        if (order == null && orderId.HasValue && orderId.Value > 0)
        {
            var candidate = await _orderService.GetOrderByIdAsync(orderId.Value);
            if (candidate != null && !candidate.Deleted &&
                candidate.PaymentMethodSystemName == StripePaymentDefaults.SystemName)
                order = candidate;
        }

        return order;
    }

    private async Task InsertOrderNoteAsync(Order order, string note)
    {
        var existingNotes = await _orderService.GetOrderNotesByOrderIdAsync(order.Id);
        if (existingNotes.Any(item => string.Equals(item.Note, note, StringComparison.Ordinal)))
            return;

        await _orderService.InsertOrderNoteAsync(new OrderNote
        {
            OrderId = order.Id,
            Note = note,
            DisplayToCustomer = false,
            CreatedOnUtc = DateTime.UtcNow
        });
    }

    private string BuildEndpointUrl()
    {
        var storeLocation = _webHelper.GetStoreLocation().TrimEnd('/');
        return $"{storeLocation}/{StripePaymentDefaults.WebhookPath}";
    }

    private static string BuildCreateIdempotencyKey(string endpointUrl)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(endpointUrl));
        return $"stripe-webhook-{Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    private static string GetStringProperty(JsonElement? rawObject, string propertyName)
    {
        if (!rawObject.HasValue || rawObject.Value.ValueKind != JsonValueKind.Object ||
            !rawObject.Value.TryGetProperty(propertyName, out var property))
            return null;

        return property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString();
    }

    private static int? ParseOrderId(Dictionary<string, string> metadata, JsonElement? rawObject)
    {
        string value = null;
        if (metadata != null)
            metadata.TryGetValue("order_id", out value);

        if (string.IsNullOrWhiteSpace(value) && rawObject.HasValue &&
            rawObject.Value.ValueKind == JsonValueKind.Object &&
            rawObject.Value.TryGetProperty("metadata", out var metadataElement) &&
            metadataElement.ValueKind == JsonValueKind.Object &&
            metadataElement.TryGetProperty("order_id", out var orderIdElement))
            value = orderIdElement.ValueKind == JsonValueKind.String ? orderIdElement.GetString() : orderIdElement.ToString();

        return int.TryParse(value, out var orderId) && orderId > 0 ? orderId : null;
    }
}
