using Nop.Core.Domain.Orders;
using Nop.Plugin.Misc.StripeBnplCore.Domain;
using Nop.Services.Payments;

namespace Nop.Plugin.Misc.StripeBnplCore.Services;

public interface IStripeBnplService
{
    Task<BnplCartEligibilityResult> EvaluateCartAsync(BnplProvider provider, IList<ShoppingCartItem> cart);
    Task<bool> ShouldHidePaymentMethodAsync(BnplProvider provider, IList<ShoppingCartItem> cart);
    Task CreateCheckoutAndRedirectAsync(BnplProvider provider, PostProcessPaymentRequest request);
    Task<bool> CanRePostProcessPaymentAsync(BnplProvider provider, Order order);
    string GetConfigurationPageUrl(BnplProvider provider);
}
