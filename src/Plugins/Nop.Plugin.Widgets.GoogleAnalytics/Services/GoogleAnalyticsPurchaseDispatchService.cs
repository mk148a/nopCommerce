using LinqToDB;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Data;
using Nop.Plugin.Widgets.GoogleAnalytics.Domains;

namespace Nop.Plugin.Widgets.GoogleAnalytics.Services;

public sealed record GoogleAnalyticsPurchaseDispatchLease(string Token, DateTime ExpiresOnUtc);

public interface IGoogleAnalyticsPurchaseDispatchService
{
    Task<GoogleAnalyticsPurchaseDispatchLease> TryReserveAsync(Order order);

    Task<bool> TryConfirmAsync(int orderId, string token);
}

/// <summary>
/// Database operations for the durable, OrderId-unique dispatch table. Keeping
/// them behind this small contract makes the compare-and-swap paths testable
/// without weakening the production repository implementation.
/// </summary>
public interface IGoogleAnalyticsPurchaseDispatchStore
{
    Task<GoogleAnalyticsPurchaseDispatch> GetByOrderIdAsync(int orderId);

    Task InsertAsync(GoogleAnalyticsPurchaseDispatch dispatch);

    Task<int> RenewExpiredLeaseAsync(GoogleAnalyticsPurchaseDispatch existing, string token, DateTime expiresOnUtc);

    Task<int> ConfirmLeaseAsync(int orderId, string token, DateTime confirmedOnUtc);
}

public static class GoogleAnalyticsPurchaseEligibility
{
    public static bool CanEmit(Order order)
    {
        return order != null &&
               order.Id > 0 &&
               !order.Deleted &&
               order.OrderStatus != OrderStatus.Cancelled &&
               order.PaymentStatus == PaymentStatus.Paid;
    }
}

/// <summary>
/// Uses the database unique OrderId index as the cross-tab/device ownership boundary.
/// Lease recovery is at-least-once: a rendered response whose acknowledgement is
/// lost may be rendered again after expiry. The canonical transaction_id remains
/// the downstream GA/GTM idempotency boundary; this service makes no external
/// exactly-once delivery claim.
/// </summary>
public sealed class GoogleAnalyticsPurchaseDispatchService : IGoogleAnalyticsPurchaseDispatchService
{
    private const string Leased = "leased";
    private const string Confirmed = "confirmed";
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(10);
    private readonly IGoogleAnalyticsPurchaseDispatchStore _store;

    public GoogleAnalyticsPurchaseDispatchService(IGoogleAnalyticsPurchaseDispatchStore store)
    {
        _store = store;
    }

    public async Task<GoogleAnalyticsPurchaseDispatchLease> TryReserveAsync(Order order)
    {
        if (!GoogleAnalyticsPurchaseEligibility.CanEmit(order))
            return null;

        var now = DateTime.UtcNow;
        var token = Guid.NewGuid().ToString("N");
        var expiry = now.Add(LeaseDuration);
        var existing = await _store.GetByOrderIdAsync(order.Id);
        if (existing == null)
        {
            try
            {
                await _store.InsertAsync(new GoogleAnalyticsPurchaseDispatch
                {
                    OrderId = order.Id,
                    Status = Leased,
                    LeaseToken = token,
                    LeaseExpiresOnUtc = expiry,
                    CreatedOnUtc = now
                });
                return new(token, expiry);
            }
            catch
            {
                // The unique OrderId index is the concurrency boundary. Re-read
                // only after a competing request has claimed the row.
                existing = await _store.GetByOrderIdAsync(order.Id);
                if (existing == null)
                    throw;
            }
        }

        if (string.Equals(existing.Status, Confirmed, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(existing.Status, Leased, StringComparison.OrdinalIgnoreCase) && existing.LeaseExpiresOnUtc > now)
            return null;

        var affected = await _store.RenewExpiredLeaseAsync(existing, token, expiry);
        return affected == 1 ? new(token, expiry) : null;
    }

    public Task<bool> TryConfirmAsync(int orderId, string token)
    {
        if (orderId <= 0 || string.IsNullOrWhiteSpace(token))
            return Task.FromResult(false);

        return ConfirmAsync(orderId, token.Trim());
    }

    private async Task<bool> ConfirmAsync(int orderId, string token)
    {
        var affected = await _store.ConfirmLeaseAsync(orderId, token, DateTime.UtcNow);
        return affected == 1;
    }
}

/// <summary>
/// LinqToDB-backed compare-and-swap implementation. The unique OrderId index
/// remains the authority when two requests both observe no existing row.
/// </summary>
public sealed class GoogleAnalyticsPurchaseDispatchStore : IGoogleAnalyticsPurchaseDispatchStore
{
    private const string Leased = "leased";
    private const string Confirmed = "confirmed";
    private readonly IRepository<GoogleAnalyticsPurchaseDispatch> _repository;

    public GoogleAnalyticsPurchaseDispatchStore(IRepository<GoogleAnalyticsPurchaseDispatch> repository)
    {
        _repository = repository;
    }

    public Task<GoogleAnalyticsPurchaseDispatch> GetByOrderIdAsync(int orderId)
    {
        return _repository.Table.FirstOrDefaultAsync(item => item.OrderId == orderId);
    }

    public Task InsertAsync(GoogleAnalyticsPurchaseDispatch dispatch)
    {
        // The dispatch record is implementation state, not a domain event.
        // Publishing an entity-insert event here can invoke unrelated handlers
        // before the browser has actually accepted the dataLayer event.
        return _repository.InsertAsync(dispatch, publishEvent: false);
    }

    public Task<int> RenewExpiredLeaseAsync(GoogleAnalyticsPurchaseDispatch existing, string token, DateTime expiresOnUtc)
    {
        return _repository.Table
            .Where(item => item.Id == existing.Id && item.Status == existing.Status &&
                           item.LeaseToken == existing.LeaseToken && item.LeaseExpiresOnUtc == existing.LeaseExpiresOnUtc)
            .Set(item => item.Status, Leased)
            .Set(item => item.LeaseToken, token)
            .Set(item => item.LeaseExpiresOnUtc, (DateTime?)expiresOnUtc)
            .Set(item => item.ConfirmedOnUtc, (DateTime?)null)
            .UpdateAsync();
    }

    public Task<int> ConfirmLeaseAsync(int orderId, string token, DateTime confirmedOnUtc)
    {
        return _repository.Table
            .Where(item => item.OrderId == orderId && item.Status == Leased && item.LeaseToken == token &&
                           item.LeaseExpiresOnUtc > confirmedOnUtc)
            .Set(item => item.Status, Confirmed)
            .Set(item => item.LeaseToken, (string)null)
            .Set(item => item.LeaseExpiresOnUtc, (DateTime?)null)
            .Set(item => item.ConfirmedOnUtc, (DateTime?)confirmedOnUtc)
            .UpdateAsync();
    }
}
