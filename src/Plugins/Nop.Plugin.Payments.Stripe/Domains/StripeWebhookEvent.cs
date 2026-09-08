using System;
using Nop.Core;

namespace Nop.Plugin.Payments.Stripe.Domains;

/// <summary>
/// Stores Stripe event identifiers so webhook retries are processed at most once.
/// </summary>
public class StripeWebhookEvent : BaseEntity
{
    public string EventId { get; set; }

    public string EventType { get; set; }

    public string PaymentIntentId { get; set; }

    public int? OrderId { get; set; }

    public DateTime ProcessedOnUtc { get; set; }

    public string ProcessingStatus { get; set; }

    public string Error { get; set; }
}
