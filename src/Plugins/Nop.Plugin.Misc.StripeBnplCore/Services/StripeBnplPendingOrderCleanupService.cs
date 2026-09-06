using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Misc.StripeBnplCore.Domain;
using Nop.Services.Logging;
using Nop.Services.Orders;

namespace Nop.Plugin.Misc.StripeBnplCore.Services;

public sealed class StripeBnplPendingOrderCleanupService : IStripeBnplPendingOrderCleanupService
{
    private static readonly BnplProvider[] Providers = Enum.GetValues<BnplProvider>();
    private readonly IOrderService _orderService;
    private readonly IOrderProcessingService _orderProcessingService;
    private readonly IStripeBnplCheckoutSessionStore _sessionStore;
    private readonly ILogger _logger;
    private readonly StripeBnplSettings _settings;

    public StripeBnplPendingOrderCleanupService(
        IOrderService orderService,
        IOrderProcessingService orderProcessingService,
        IStripeBnplCheckoutSessionStore sessionStore,
        ILogger logger,
        StripeBnplSettings settings)
    {
        _orderService = orderService;
        _orderProcessingService = orderProcessingService;
        _sessionStore = sessionStore;
        _logger = logger;
        _settings = settings;
    }

    public async Task<StripeBnplPendingOrderCleanupResult> SweepAsync(int maxOrders = 100)
    {
        if (maxOrders is < 1 or > 500)
            throw new ArgumentOutOfRangeException(nameof(maxOrders));

        var candidates = new Dictionary<int, (Order Order, BnplProvider Provider)>();
        foreach (var provider in Providers)
        {
            var remaining = maxOrders - candidates.Count;
            if (remaining <= 0)
                break;

            var orders = await _orderService.SearchOrdersAsync(
                paymentMethodSystemName: StripeBnplDefaults.GetSystemName(provider),
                osIds: new List<int> { (int)OrderStatus.Pending },
                psIds: new List<int> { (int)PaymentStatus.Pending, (int)PaymentStatus.Voided },
                pageSize: remaining);
            foreach (var order in orders)
                candidates.TryAdd(order.Id, (order, provider));
        }

        var cancelled = 0;
        var skipped = 0;
        var failed = 0;
        foreach (var candidate in candidates.Values)
        {
            try
            {
                if (await TryCancelAsync(candidate.Order.Id, candidate.Provider,
                        "scheduled_cleanup", "scheduled_task"))
                    cancelled++;
                else
                    skipped++;
            }
            catch (Exception exception)
            {
                failed++;
                await _logger.ErrorAsync(
                    $"[Stripe BNPL] Pending order cleanup failed for order {candidate.Order.Id}.", exception);
            }
        }

        return new(candidates.Count, cancelled, skipped, failed);
    }

    public async Task<bool> TryCancelAsync(int orderId, BnplProvider provider, string reason,
        string source, StripeBnplCheckoutSession knownSession = null)
    {
        if (orderId <= 0 || !Enum.IsDefined(provider))
            return false;

        var order = await _orderService.GetOrderByIdAsync(orderId);
        if (order == null || order.Deleted || order.OrderStatus == OrderStatus.Cancelled ||
            order.PaymentStatus == PaymentStatus.Paid || order.PaymentStatus == PaymentStatus.Authorized)
            return false;
        if (!string.Equals(order.PaymentMethodSystemName, StripeBnplDefaults.GetSystemName(provider),
                StringComparison.OrdinalIgnoreCase))
            return false;
        if (order.PaymentStatus is not (PaymentStatus.Pending or PaymentStatus.Voided))
            return false;

        var session = await _sessionStore.GetLatestAsync(orderId, provider) ?? knownSession;
        var now = DateTime.UtcNow;
        var cutoff = now.AddHours(-GetCancellationHours());
        if (!IsCancellationDue(session, order.CreatedOnUtc, now, cutoff))
            return false;

        // Re-read the latest attempt after the order read so an immediately-created
        // retry protects the order from a stale webhook or scheduler candidate.
        var latest = await _sessionStore.GetLatestAsync(orderId, provider) ?? session;
        if (!IsCancellationDue(latest, order.CreatedOnUtc, now, cutoff))
            return false;
        if (latest != null && string.Equals(latest.Status, "paid", StringComparison.OrdinalIgnoreCase))
            return false;

        // Re-read the order after the session check so a concurrent successful
        // webhook wins the race and prevents cancellation.
        order = await _orderService.GetOrderByIdAsync(orderId);
        if (order == null || order.Deleted || order.OrderStatus == OrderStatus.Cancelled ||
            order.PaymentStatus is PaymentStatus.Paid or PaymentStatus.Authorized ||
            !string.Equals(order.PaymentMethodSystemName, StripeBnplDefaults.GetSystemName(provider),
                StringComparison.OrdinalIgnoreCase) ||
            order.PaymentStatus is not (PaymentStatus.Pending or PaymentStatus.Voided))
            return false;

        if (order.PaymentStatus == PaymentStatus.Pending)
        {
            order.PaymentStatus = PaymentStatus.Voided;
            await _orderService.UpdateOrderAsync(order);
        }

        if (order.OrderStatus != OrderStatus.Cancelled)
            await _orderProcessingService.CancelOrderAsync(order, true);

        var note = $"Stripe BNPL order automatically cancelled ({reason}, source {source})" +
                   (latest == null ? "." : $" for Session {latest.SessionId}.");
        await InsertOrderNoteOnceAsync(order.Id, note);
        await _logger.InformationAsync($"[Stripe BNPL] Order {order.CustomOrderNumber} cancelled: {note}");
        return true;
    }

    private int GetCancellationHours() => Math.Clamp(
        _settings.PendingOrderCancellationHours <= 0 ? 24 : _settings.PendingOrderCancellationHours, 1, 168);

    private static bool IsCancellationDue(StripeBnplCheckoutSession session, DateTime orderCreatedOnUtc,
        DateTime now, DateTime cutoff)
    {
        if (session == null)
            return orderCreatedOnUtc <= cutoff;
        if (string.Equals(session.Status, "paid", StringComparison.OrdinalIgnoreCase))
            return false;
        if (session.Status is "expired" or "canceled" or "cancelled" or "payment_failed")
            return true;
        if (session.Status is "open" or "creating")
            return session.ExpiresOnUtc <= now;
        return session.UpdatedOnUtc <= cutoff;
    }

    private async Task InsertOrderNoteOnceAsync(int orderId, string note)
    {
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
}
