using Nop.Core.Domain.Orders;

namespace Nop.Plugin.Misc.StripeBnplCore.Services;

public sealed record BnplCartContext(string CustomerCountryIso2, string CurrencyCode, decimal Amount);

public interface IBnplCartContextService
{
    Task<BnplCartContext> GetAsync(IList<ShoppingCartItem> cart);
    Task<BnplCartContext> GetAsync(Order order);
}
