using Nop.Core.Domain.Orders;
using Nop.Plugin.Misc.StripeBnplCore.Domain;

namespace Nop.Plugin.Misc.StripeBnplCore.Services;

public interface IStripeBnplCheckoutService
{
    Task<StripeBnplCheckoutResult> CreateAsync(BnplProvider provider, Order order);
    Task<StripeBnplCheckoutResult> GetAsync(string sessionId);
}

public sealed record StripeBnplCheckoutResult(
    string SessionId,
    string Url,
    string PaymentIntentId,
    long AmountMinor,
    string Currency,
    string Status);
