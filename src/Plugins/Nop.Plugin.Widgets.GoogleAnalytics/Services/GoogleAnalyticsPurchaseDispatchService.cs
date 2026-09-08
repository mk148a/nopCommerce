using LinqToDB;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Data;
using Nop.Plugin.Widgets.GoogleAnalytics.Domains;
using Nop.Services.Common;

namespace Nop.Plugin.Widgets.GoogleAnalytics.Services;

public interface IGoogleAnalyticsPurchaseDispatchService
{
    /// <summary>
    /// Atomically reserves the sole browser-side purchase dispatch for a paid order.
    /// </summary>
    Task<bool> TryReserveAsync(Order order);
}

public static class GoogleAnalyticsPurchaseEligibility
{
    /// <summary>
    /// A conversion may only be emitted for a live, non-cancelled order whose
    /// payment has been recorded as paid by the server.
    /// </summary>
    public static bool CanEmit(Order order)
    {
        return order != null &&
               order.Id > 0 &&
               !order.Deleted &&
               order.OrderStatus != OrderStatus.Cancelled &&
               order.PaymentStatus == PaymentStatus.Paid;
    }
}

public sealed class GoogleAnalyticsPurchaseDispatchService : IGoogleAnalyticsPurchaseDispatchService
{
    private const string StripePaymentSystemName = "Payments.Stripe";

    private readonly IRepository<GoogleAnalyticsPurchaseDispatch> _dispatchRepository;
    private readonly IGenericAttributeService _genericAttributeService;

    public GoogleAnalyticsPurchaseDispatchService(IRepository<GoogleAnalyticsPurchaseDispatch> dispatchRepository,
        IGenericAttributeService genericAttributeService)
    {
        _dispatchRepository = dispatchRepository;
        _genericAttributeService = genericAttributeService;
    }

    public async Task<bool> TryReserveAsync(Order order)
    {
        if (!GoogleAnalyticsPurchaseEligibility.CanEmit(order))
            return false;

        // A Stripe/BNPL browser return is never proof of payment. The Stripe
        // plugin writes this marker only after it validates payment_intent.succeeded
        // with the configured webhook signing secret.
        if (string.Equals(order.PaymentMethodSystemName, StripePaymentSystemName, StringComparison.OrdinalIgnoreCase))
        {
            var verifiedPaymentIntent = await _genericAttributeService.GetAttributeAsync<string>(order,
                "GoogleAnalytics.StripeWebhookVerifiedPaymentIntent", order.StoreId);
            if (string.IsNullOrWhiteSpace(verifiedPaymentIntent))
                return false;
        }

        if (await ExistsAsync(order.Id))
            return false;

        try
        {
            await _dispatchRepository.InsertAsync(new GoogleAnalyticsPurchaseDispatch
            {
                OrderId = order.Id,
                CreatedOnUtc = DateTime.UtcNow
            }, publishEvent: false);

            return true;
        }
        catch
        {
            // The unique OrderId index is the concurrency boundary. If another
            // request claimed the order first, this request must stay silent.
            if (await ExistsAsync(order.Id))
                return false;

            throw;
        }
    }

    private Task<bool> ExistsAsync(int orderId)
    {
        return _dispatchRepository.Table.AnyAsync(record => record.OrderId == orderId);
    }
}
