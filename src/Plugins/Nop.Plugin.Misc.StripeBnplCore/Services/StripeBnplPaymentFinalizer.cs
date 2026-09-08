using System.Collections.Concurrent;
using System.Text.Json;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Misc.StripeBnplCore.Domain;
using Nop.Services.Directory;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Stripe;
using Stripe.Checkout;

namespace Nop.Plugin.Misc.StripeBnplCore.Services;

public sealed class StripeBnplPaymentFinalizer : IStripeBnplPaymentFinalizer
{
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> OrderLocks = new();

    private readonly ICurrencyService _currencyService;
    private readonly IOrderProcessingService _orderProcessingService;
    private readonly IOrderService _orderService;
    private readonly IStripeBnplCheckoutSessionStore _sessionStore;
    private readonly IStripeBnplConversionService _conversionService;
    private readonly IStripeBnplEnvironmentGuard _environmentGuard;
    private readonly IStripeBnplPaymentClient _paymentClient;
    private readonly IStripeBnplPaymentRecordStore _paymentRecordStore;
    private readonly IStripeBnplSessionClient _sessionClient;
    private readonly ILogger _logger;

    public StripeBnplPaymentFinalizer(
        ICurrencyService currencyService,
        IOrderProcessingService orderProcessingService,
        IOrderService orderService,
        IStripeBnplCheckoutSessionStore sessionStore,
        IStripeBnplConversionService conversionService,
        IStripeBnplEnvironmentGuard environmentGuard,
        IStripeBnplPaymentClient paymentClient,
        IStripeBnplPaymentRecordStore paymentRecordStore,
        IStripeBnplSessionClient sessionClient,
        ILogger logger)
    {
        _currencyService = currencyService;
        _orderProcessingService = orderProcessingService;
        _orderService = orderService;
        _sessionStore = sessionStore;
        _conversionService = conversionService;
        _environmentGuard = environmentGuard;
        _paymentClient = paymentClient;
        _paymentRecordStore = paymentRecordStore;
        _sessionClient = sessionClient;
        _logger = logger;
    }

    public async Task<StripeBnplFinalizationResult> FinalizeSessionAsync(string sessionId,
        Guid? expectedOrderGuid = null, string source = "webhook")
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("Stripe Checkout Session id is required.", nameof(sessionId));

        _environmentGuard.EnsureSafe();
        var internalSession = await _sessionStore.GetBySessionIdAsync(sessionId)
            ?? throw new NopException("Stripe Checkout Session is not registered by this store.");
        var stripeSession = await _sessionClient.GetAsync(sessionId,
            new SessionGetOptions { Expand = new List<string> { "payment_intent" } });

        if (stripeSession == null || !string.Equals(stripeSession.Id, internalSession.SessionId, StringComparison.Ordinal))
            throw new NopException("Stripe Checkout Session identity mismatch.");
        if (internalSession.IsSandbox == stripeSession.Livemode)
            throw new NopException("Stripe Checkout Session environment mismatch.");

        var orderId = ParsePositiveInt(stripeSession.Metadata, "order_id");
        var orderGuid = ParseGuid(stripeSession.Metadata, "order_guid");
        if (orderId != internalSession.OrderId || orderGuid == Guid.Empty)
            throw new NopException("Stripe Checkout Session order metadata mismatch.");
        if (expectedOrderGuid.HasValue && expectedOrderGuid.Value != orderGuid)
            throw new NopException("Stripe return order identity mismatch.");
        if (!TryGetMetadata(stripeSession.Metadata, "provider", out var providerSlug) ||
            !StripeBnplDefaults.TryParseProviderSlug(providerSlug, out var provider) ||
            provider != internalSession.Provider)
            throw new NopException("Stripe Checkout Session provider metadata mismatch.");
        if (!TryGetMetadata(stripeSession.Metadata, "eligibility_snapshot_hash", out var stripeSnapshotHash) ||
            !string.Equals(stripeSnapshotHash, internalSession.EligibilitySnapshotHash,
                StringComparison.OrdinalIgnoreCase))
            throw new NopException("Stripe Checkout Session eligibility snapshot metadata mismatch.");

        var gate = OrderLocks.GetOrAdd(orderId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try
        {
            var order = await _orderService.GetOrderByIdAsync(orderId)
                ?? throw new NopException("nopCommerce order was not found.");
            ValidateOrderIdentity(order, orderGuid, provider);
            var orderItems = await _orderService.GetOrderItemsAsync(order.Id);
            StripeBnplEligibilitySnapshotCodec.ValidateForFinalization(internalSession, order, orderItems);

            var expectedCurrency = (order.CustomerCurrencyCode ?? string.Empty).Trim().ToLowerInvariant();
            var expectedValue = _currencyService.ConvertCurrency(order.OrderTotal, order.CurrencyRate);
            var expectedAmount = StripeBnplCheckoutService.ToMinorUnits(expectedValue, expectedCurrency);
            if (stripeSession.AmountTotal != expectedAmount ||
                !string.Equals(stripeSession.Currency, expectedCurrency, StringComparison.OrdinalIgnoreCase) ||
                internalSession.AmountMinor != expectedAmount ||
                !string.Equals(internalSession.Currency, expectedCurrency, StringComparison.OrdinalIgnoreCase))
                throw new NopException("Stripe paid amount or currency does not match the nopCommerce order.");

            if (!string.Equals(stripeSession.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase))
            {
                await _sessionStore.UpdateStatusAsync(internalSession,
                    stripeSession.Status ?? stripeSession.PaymentStatus, stripeSession.PaymentIntentId);
                return new(order.Id, false, false, stripeSession.PaymentStatus, "Stripe payment is not paid.");
            }

            if (string.IsNullOrWhiteSpace(stripeSession.PaymentIntentId))
                throw new NopException("Paid Stripe Checkout Session has no PaymentIntent.");

            var paymentIntent = await _paymentClient.GetPaymentIntentAsync(stripeSession.PaymentIntentId);
            ValidatePaymentIntent(paymentIntent, order, provider, expectedAmount, expectedCurrency,
                internalSession.EligibilitySnapshotHash);
            var actualPaymentMethod = paymentIntent.PaymentMethod?.Type ??
                                      paymentIntent.LatestCharge?.PaymentMethodDetails?.Type;
            var snapshot = CreatePaymentSnapshot(order, provider, stripeSession, paymentIntent, actualPaymentMethod);
            var claim = await _paymentRecordStore.TryAcquireFinalizationAsync(snapshot);
            if (claim == StripeBnplFinalizationClaim.InProgress)
                return new(order.Id, false, false, "processing", "Another worker is finalizing this payment.");
            if (claim == StripeBnplFinalizationClaim.Terminal)
                return new(order.Id, false, true, "terminal",
                    "The payment already has a refund or dispute lifecycle state and was not reopened.");

            order = await _orderService.GetOrderByIdAsync(order.Id)
                ?? throw new NopException("nopCommerce order disappeared during payment finalization.");
            if (claim == StripeBnplFinalizationClaim.AlreadyPaid && order.PaymentStatus != PaymentStatus.Paid)
                throw new NopException("Stripe finalization claim and nopCommerce paid state are inconsistent.");
            var alreadyPaid = order.PaymentStatus == PaymentStatus.Paid;

            try
            {
                if (!alreadyPaid)
                {
                    if (order.OrderStatus == OrderStatus.Cancelled ||
                        order.PaymentStatus is PaymentStatus.Voided or PaymentStatus.Refunded)
                        throw new NopException("A terminal nopCommerce order cannot be reopened by a late Stripe event.");

                    order.AuthorizationTransactionId = paymentIntent.Id;
                    order.AuthorizationTransactionResult = paymentIntent.Status;
                    order.CaptureTransactionId = paymentIntent.LatestChargeId;
                    order.CaptureTransactionResult = paymentIntent.Status;
                    await _orderService.UpdateOrderAsync(order);
                    await _orderProcessingService.MarkOrderAsPaidAsync(order);
                    await InsertOrderNoteOnceAsync(order,
                        $"Stripe BNPL payment finalized from {source}: {provider}, Session {stripeSession.Id}, PaymentIntent {paymentIntent.Id}.");
                }

                await _sessionStore.UpdateStatusAsync(internalSession, "paid", paymentIntent.Id);
                await _conversionService.EnsureReadyAsync(order, provider, actualPaymentMethod, expectedValue);
                if (claim == StripeBnplFinalizationClaim.Acquired)
                    await _paymentRecordStore.CompleteFinalizationAsync(snapshot);
            }
            catch
            {
                if (claim == StripeBnplFinalizationClaim.Acquired)
                    await _paymentRecordStore.FailFinalizationAsync(order.Id);
                throw;
            }

            await _logger.InformationAsync(
                $"[Stripe BNPL] Order {order.CustomOrderNumber} finalized as paid from {source}; provider {provider}, session {stripeSession.Id}.");
            return new(order.Id, true, alreadyPaid, "paid", alreadyPaid ? "Order was already paid." : "Order marked paid.");
        }
        finally
        {
            gate.Release();
        }
    }

    private static void ValidateOrderIdentity(Order order, Guid orderGuid, BnplProvider provider)
    {
        if (order.Deleted || order.OrderGuid != orderGuid)
            throw new NopException("nopCommerce order identity mismatch.");
        if (!string.Equals(order.PaymentMethodSystemName, StripeBnplDefaults.GetSystemName(provider),
                StringComparison.OrdinalIgnoreCase))
            throw new NopException("nopCommerce order payment provider mismatch.");
    }

    private static void ValidatePaymentIntent(PaymentIntent paymentIntent, Order order, BnplProvider provider,
        long expectedAmount, string expectedCurrency, string eligibilitySnapshotHash)
    {
        if (paymentIntent == null || !string.Equals(paymentIntent.Status, "succeeded", StringComparison.OrdinalIgnoreCase))
            throw new NopException("Stripe PaymentIntent is not succeeded.");
        if (paymentIntent.Amount != expectedAmount ||
            !string.Equals(paymentIntent.Currency, expectedCurrency, StringComparison.OrdinalIgnoreCase))
            throw new NopException("Stripe PaymentIntent total or currency mismatch.");
        if (ParsePositiveInt(paymentIntent.Metadata, "order_id") != order.Id ||
            ParseGuid(paymentIntent.Metadata, "order_guid") != order.OrderGuid ||
            !TryGetMetadata(paymentIntent.Metadata, "eligibility_snapshot_hash", out var paymentSnapshotHash) ||
            !string.Equals(paymentSnapshotHash, eligibilitySnapshotHash, StringComparison.OrdinalIgnoreCase))
            throw new NopException("Stripe PaymentIntent order metadata mismatch.");

        var type = paymentIntent.PaymentMethod?.Type ?? paymentIntent.LatestCharge?.PaymentMethodDetails?.Type;
        if (!string.Equals(type, StripeBnplDefaults.GetProviderSlug(provider), StringComparison.OrdinalIgnoreCase))
            throw new NopException("The actual Stripe payment method does not match the selected BNPL provider.");
    }

    private static StripeBnplPaymentSnapshot CreatePaymentSnapshot(Order order, BnplProvider provider,
        Session session, PaymentIntent paymentIntent, string paymentMethodType)
    {
        var charge = paymentIntent.LatestCharge;
        var balance = charge?.BalanceTransaction;
        var hasFeeData = !string.IsNullOrWhiteSpace(balance?.Id);
        var feeDetails = balance?.FeeDetails?.Select(item => new
        {
            item.Amount,
            item.Currency,
            item.Type,
            item.Description,
            item.Application
        }).ToArray();

        return new StripeBnplPaymentSnapshot(
            order.Id,
            provider,
            session.Id,
            paymentIntent.Id,
            charge?.Id,
            balance?.Id,
            paymentMethodType,
            "paid",
            paymentIntent.Amount,
            hasFeeData ? balance.Fee : null,
            hasFeeData ? balance.Net : null,
            paymentIntent.Currency,
            balance?.Currency,
            balance?.ExchangeRate,
            feeDetails == null ? null : JsonSerializer.Serialize(feeDetails),
            hasFeeData ? StripeBnplFeeDataStatus.Complete : StripeBnplFeeDataStatus.Pending,
            !session.Livemode);
    }

    private async Task InsertOrderNoteOnceAsync(Order order, string note)
    {
        var notes = await _orderService.GetOrderNotesByOrderIdAsync(order.Id);
        if (notes.Any(item => string.Equals(item.Note, note, StringComparison.Ordinal)))
            return;
        await _orderService.InsertOrderNoteAsync(new OrderNote
        {
            OrderId = order.Id,
            Note = note,
            DisplayToCustomer = false,
            CreatedOnUtc = DateTime.UtcNow
        });
    }

    private static int ParsePositiveInt(Dictionary<string, string> metadata, string key)
    {
        return TryGetMetadata(metadata, key, out var value) && int.TryParse(value, out var result) && result > 0
            ? result
            : 0;
    }

    private static Guid ParseGuid(Dictionary<string, string> metadata, string key)
    {
        return TryGetMetadata(metadata, key, out var value) && Guid.TryParse(value, out var result)
            ? result
            : Guid.Empty;
    }

    private static bool TryGetMetadata(Dictionary<string, string> metadata, string key, out string value)
    {
        value = null;
        return metadata != null && metadata.TryGetValue(key, out value) && !string.IsNullOrWhiteSpace(value);
    }
}
