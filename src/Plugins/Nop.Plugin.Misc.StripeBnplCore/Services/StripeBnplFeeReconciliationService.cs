using System.Text.Json;
using Nop.Core;
using Nop.Plugin.Misc.StripeBnplCore.Domain;
using Nop.Services.Logging;
using Stripe;

namespace Nop.Plugin.Misc.StripeBnplCore.Services;

public sealed class StripeBnplFeeReconciliationService : IStripeBnplFeeReconciliationService
{
    internal const int MaximumAttempts = 10;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ClaimTimeout = TimeSpan.FromMinutes(15);

    private readonly IStripeBnplEnvironmentGuard _environmentGuard;
    private readonly IStripeBnplPaymentClient _paymentClient;
    private readonly IStripeBnplPaymentRecordStore _paymentRecordStore;
    private readonly ILogger _logger;

    public StripeBnplFeeReconciliationService(
        IStripeBnplEnvironmentGuard environmentGuard,
        IStripeBnplPaymentClient paymentClient,
        IStripeBnplPaymentRecordStore paymentRecordStore,
        ILogger logger)
    {
        _environmentGuard = environmentGuard;
        _paymentClient = paymentClient;
        _paymentRecordStore = paymentRecordStore;
        _logger = logger;
    }

    public async Task<StripeBnplFeeReconciliationResult> ReconcileAsync(int maxRecords = 50)
    {
        if (maxRecords is < 1 or > 500)
            throw new ArgumentOutOfRangeException(nameof(maxRecords), "Batch size must be between 1 and 500.");

        _environmentGuard.EnsureSafe();
        var now = DateTime.UtcNow;
        var candidates = await _paymentRecordStore.GetFeeReconciliationCandidatesAsync(maxRecords,
            now.Subtract(RetryDelay), MaximumAttempts);
        var completed = 0;
        var failed = 0;
        var skipped = 0;

        foreach (var record in candidates)
        {
            var claim = await _paymentRecordStore.TryAcquireFeeReconciliationAsync(record.Id,
                DateTime.UtcNow.Subtract(ClaimTimeout), MaximumAttempts);
            if (claim != StripeBnplFeeReconciliationClaim.Acquired)
            {
                skipped++;
                continue;
            }

            try
            {
                var paymentIntent = await _paymentClient.GetPaymentIntentAsync(record.PaymentIntentId);
                var snapshot = CreateFeeSnapshot(record, paymentIntent);
                await _paymentRecordStore.CompleteFeeReconciliationAsync(record.Id, snapshot);
                completed++;
            }
            catch (Exception exception)
            {
                failed++;
                var message = GetSafeError(exception);
                await _paymentRecordStore.FailFeeReconciliationAsync(record.Id, message);
                await _logger.WarningAsync(
                    $"[Stripe BNPL] Fee reconciliation for payment record {record.Id}, order {record.OrderId} failed: {message}",
                    exception);
            }
        }

        return new(candidates.Count, completed, failed, skipped);
    }

    internal static StripeBnplFeeSnapshot CreateFeeSnapshot(StripeBnplPaymentRecord record,
        PaymentIntent paymentIntent)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (paymentIntent == null || !string.Equals(paymentIntent.Id, record.PaymentIntentId,
                StringComparison.Ordinal))
            throw new NopException("Stripe PaymentIntent identity does not match the fee reconciliation record.");
        if (!string.Equals(paymentIntent.Status, "succeeded", StringComparison.OrdinalIgnoreCase) ||
            paymentIntent.Amount != record.AmountMinor ||
            !string.Equals(paymentIntent.Currency, record.Currency, StringComparison.OrdinalIgnoreCase))
            throw new NopException("Stripe PaymentIntent amount, currency or status does not match the payment record.");
        if (paymentIntent.Livemode == record.IsSandbox)
            throw new NopException("Stripe PaymentIntent environment does not match the payment record.");
        if (!TryGetOrderId(paymentIntent.Metadata, out var orderId) || orderId != record.OrderId)
            throw new NopException("Stripe PaymentIntent order metadata does not match the payment record.");

        var paymentMethodType = paymentIntent.PaymentMethod?.Type ??
                                paymentIntent.LatestCharge?.PaymentMethodDetails?.Type;
        if (!string.Equals(paymentMethodType, StripeBnplDefaults.GetProviderSlug(record.Provider),
                StringComparison.OrdinalIgnoreCase))
            throw new NopException("Stripe PaymentIntent provider does not match the payment record.");

        var charge = paymentIntent.LatestCharge;
        if (charge == null || string.IsNullOrWhiteSpace(charge.Id))
            throw new NopException("Stripe latest charge is not available yet.");
        if (!string.IsNullOrWhiteSpace(record.ChargeId) &&
            !string.Equals(record.ChargeId, charge.Id, StringComparison.Ordinal))
            throw new NopException("Stripe latest charge does not match the payment record.");

        var balance = charge.BalanceTransaction;
        if (balance == null || string.IsNullOrWhiteSpace(balance.Id))
            throw new NopException("Stripe BalanceTransaction fee data is not available yet.");
        if (string.IsNullOrWhiteSpace(balance.Currency))
            throw new NopException("Stripe BalanceTransaction settlement currency is missing.");

        var feeDetails = balance.FeeDetails?.Select(item => new
        {
            item.Amount,
            item.Currency,
            item.Type,
            item.Description,
            item.Application
        }).ToArray();

        return new StripeBnplFeeSnapshot(
            charge.Id,
            balance.Id,
            balance.Fee,
            balance.Net,
            balance.Currency.Trim().ToLowerInvariant(),
            balance.ExchangeRate,
            feeDetails == null ? null : JsonSerializer.Serialize(feeDetails));
    }

    private static bool TryGetOrderId(Dictionary<string, string> metadata, out int orderId)
    {
        orderId = 0;
        return metadata != null && metadata.TryGetValue("order_id", out var value) &&
               int.TryParse(value, out orderId) && orderId > 0;
    }

    private static string GetSafeError(Exception exception)
    {
        var message = exception is StripeException stripeException
            ? stripeException.StripeError?.Message ?? stripeException.Message
            : exception.Message;
        if (string.IsNullOrWhiteSpace(message))
            return "Stripe fee reconciliation failed without an error message.";
        message = message.Trim();
        return message.Length <= 1000 ? message : message[..1000];
    }
}
