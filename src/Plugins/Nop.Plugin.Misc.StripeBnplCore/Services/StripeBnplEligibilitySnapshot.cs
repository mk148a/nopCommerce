using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Misc.StripeBnplCore.Domain;

namespace Nop.Plugin.Misc.StripeBnplCore.Services;

public sealed record StripeBnplEligibilitySnapshotEnvelope(string Json, string Sha256);

public sealed record StripeBnplEligibilitySnapshot(
    int Version,
    int OrderId,
    Guid OrderGuid,
    BnplProvider Provider,
    string CustomerCountryIso2,
    string Currency,
    long AmountMinor,
    IReadOnlyList<StripeBnplEligibilitySnapshotItem> Items);

public sealed record StripeBnplEligibilitySnapshotItem(
    int OrderItemId,
    int ProductId,
    int Quantity,
    string AttributesXml,
    int EligibilityRecordId,
    BnplEligibilityState EligibilityState,
    string Reason,
    int? FulfillmentDays,
    string ApprovalReference,
    string PolicyVersion,
    DateTime EligibilityUpdatedOnUtc,
    string RestrictedTerm);

public static class StripeBnplEligibilitySnapshotCodec
{
    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static StripeBnplEligibilitySnapshotEnvelope Create(StripeBnplEligibilitySnapshot snapshot)
    {
        ValidateEvidence(snapshot);
        var json = JsonSerializer.Serialize(snapshot, SerializerOptions);
        return new StripeBnplEligibilitySnapshotEnvelope(json, ComputeHash(json));
    }

    public static StripeBnplEligibilitySnapshot ValidateForFinalization(
        StripeBnplCheckoutSession session,
        Order order,
        IList<OrderItem> orderItems)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(orderItems);

        if (string.IsNullOrWhiteSpace(session.EligibilitySnapshotJson) ||
            string.IsNullOrWhiteSpace(session.EligibilitySnapshotHash))
            throw new NopException("Stripe BNPL eligibility snapshot is missing.");

        var computedHash = ComputeHash(session.EligibilitySnapshotJson);
        if (!HashesMatch(computedHash, session.EligibilitySnapshotHash))
            throw new NopException("Stripe BNPL eligibility snapshot integrity check failed.");

        StripeBnplEligibilitySnapshot snapshot;
        try
        {
            snapshot = JsonSerializer.Deserialize<StripeBnplEligibilitySnapshot>(
                session.EligibilitySnapshotJson, SerializerOptions);
        }
        catch (JsonException exception)
        {
            throw new NopException("Stripe BNPL eligibility snapshot cannot be parsed.", exception);
        }

        ValidateEvidence(snapshot);
        if (snapshot.OrderId != order.Id || snapshot.OrderGuid != order.OrderGuid ||
            snapshot.Provider != session.Provider)
            throw new NopException("Stripe BNPL eligibility snapshot order or provider identity mismatch.");
        if (snapshot.AmountMinor != session.AmountMinor ||
            !string.Equals(snapshot.Currency, session.Currency, StringComparison.OrdinalIgnoreCase))
            throw new NopException("Stripe BNPL eligibility snapshot amount or currency mismatch.");

        var currentItems = orderItems
            .OrderBy(item => item.Id)
            .ThenBy(item => item.ProductId)
            .Select(item => new
            {
                OrderItemId = item.Id,
                item.ProductId,
                item.Quantity,
                AttributesXml = item.AttributesXml ?? string.Empty
            })
            .ToArray();
        var capturedItems = snapshot.Items
            .OrderBy(item => item.OrderItemId)
            .ThenBy(item => item.ProductId)
            .ToArray();

        if (currentItems.Length != capturedItems.Length)
            throw new NopException("Stripe BNPL order items no longer match the eligibility snapshot.");

        for (var index = 0; index < currentItems.Length; index++)
        {
            var current = currentItems[index];
            var captured = capturedItems[index];
            if (current.OrderItemId != captured.OrderItemId || current.ProductId != captured.ProductId ||
                current.Quantity != captured.Quantity ||
                !string.Equals(current.AttributesXml, captured.AttributesXml, StringComparison.Ordinal))
                throw new NopException("Stripe BNPL order items or product attributes no longer match the eligibility snapshot.");
        }

        return snapshot;
    }

    public static string ComputeHash(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("Eligibility snapshot JSON is required.", nameof(json));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
    }

    private static bool HashesMatch(string left, string right)
    {
        try
        {
            var leftBytes = Convert.FromHexString(left);
            var rightBytes = Convert.FromHexString(right.Trim());
            return leftBytes.Length == rightBytes.Length &&
                   CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static void ValidateEvidence(StripeBnplEligibilitySnapshot snapshot)
    {
        if (snapshot == null || snapshot.Version != CurrentVersion || snapshot.OrderId <= 0 ||
            snapshot.OrderGuid == Guid.Empty || !Enum.IsDefined(snapshot.Provider) ||
            string.IsNullOrWhiteSpace(snapshot.CustomerCountryIso2) ||
            string.IsNullOrWhiteSpace(snapshot.Currency) || snapshot.AmountMinor <= 0 ||
            snapshot.Items == null || snapshot.Items.Count == 0)
            throw new NopException("Stripe BNPL eligibility snapshot is incomplete or unsupported.");

        foreach (var item in snapshot.Items)
        {
            if (item.OrderItemId <= 0 || item.ProductId <= 0 || item.Quantity <= 0 ||
                item.EligibilityRecordId <= 0 || item.EligibilityUpdatedOnUtc == default)
                throw new NopException("Stripe BNPL eligibility snapshot contains incomplete product evidence.");
            if (item.EligibilityState != BnplEligibilityState.Allowed)
                throw new NopException(
                    $"Stripe BNPL eligibility snapshot contains {item.EligibilityState} product evidence.");
            if (snapshot.Provider == BnplProvider.Afterpay &&
                item.FulfillmentDays is < 0)
                throw new NopException("Stripe BNPL eligibility snapshot has invalid Afterpay fulfillment evidence.");
            if (!string.IsNullOrWhiteSpace(item.RestrictedTerm) &&
                string.IsNullOrWhiteSpace(item.ApprovalReference))
                throw new NopException("Stripe BNPL eligibility snapshot has restricted catalog evidence without written approval.");
        }
    }
}
