using Nop.Core.Domain.Orders;
using Nop.Plugin.Misc.StripeBnplCore.Domain;

namespace Nop.Plugin.Misc.StripeBnplCore.Services;

public interface IStripeBnplConversionService
{
    Task<StripeBnplConversionRecord> EnsureReadyAsync(Order order, BnplProvider provider, string paymentType,
        decimal? value = null);
    Task<StripeBnplConversionRecord> GetByOrderIdAsync(int orderId);
    Task MarkConsumedAsync(int orderId);
}
