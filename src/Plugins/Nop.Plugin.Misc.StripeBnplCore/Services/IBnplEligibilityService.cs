using Nop.Core.Domain.Orders;
using Nop.Plugin.Misc.StripeBnplCore.Domain;

namespace Nop.Plugin.Misc.StripeBnplCore.Services;

public interface IBnplEligibilityService
{
    Task<BnplCartEligibilityResult> EvaluateCartAsync(BnplProvider provider, IList<ShoppingCartItem> cart);
    Task<BnplCartEligibilityResult> EvaluateOrderAsync(BnplProvider provider, Order order, IList<OrderItem> orderItems);
    Task<StripeBnplEligibilitySnapshotEnvelope> CaptureOrderSnapshotAsync(BnplProvider provider, Order order,
        IList<OrderItem> orderItems, long amountMinor, string currency);
}
