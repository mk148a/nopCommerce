using LinqToDB;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Data;
using Nop.Plugin.Misc.StripeBnplCore.Domain;

namespace Nop.Plugin.Misc.StripeBnplCore.Services;

public sealed class StripeBnplConversionService : IStripeBnplConversionService
{
    private readonly IRepository<StripeBnplConversionRecord> _repository;

    public StripeBnplConversionService(IRepository<StripeBnplConversionRecord> repository)
    {
        _repository = repository;
    }

    public async Task<StripeBnplConversionRecord> EnsureReadyAsync(Order order, BnplProvider provider, string paymentType,
        decimal? value = null)
    {
        ArgumentNullException.ThrowIfNull(order);
        if (order.Deleted || order.OrderStatus == OrderStatus.Cancelled || order.PaymentStatus != PaymentStatus.Paid)
            throw new InvalidOperationException("A conversion record can only be created for a paid, active order.");
        var existing = await GetByOrderIdAsync(order.Id);
        if (existing != null)
            return existing;

        var record = new StripeBnplConversionRecord
        {
            OrderId = order.Id,
            Provider = provider,
            TransactionId = order.CustomOrderNumber ?? order.Id.ToString(),
            PaymentType = paymentType,
            Value = value ?? order.OrderTotal,
            Currency = order.CustomerCurrencyCode,
            Status = "ready",
            CreatedOnUtc = DateTime.UtcNow
        };

        try
        {
            await _repository.InsertAsync(record, publishEvent: false);
            return record;
        }
        catch
        {
            var concurrent = await GetByOrderIdAsync(order.Id);
            if (concurrent != null)
                return concurrent;
            throw;
        }
    }

    public Task<StripeBnplConversionRecord> GetByOrderIdAsync(int orderId) =>
        _repository.Table.FirstOrDefaultAsync(item => item.OrderId == orderId);

    public async Task MarkConsumedAsync(int orderId)
    {
        var record = await GetByOrderIdAsync(orderId);
        if (record == null || string.Equals(record.Status, "consumed", StringComparison.OrdinalIgnoreCase))
            return;
        record.Status = "consumed";
        record.ConsumedOnUtc = DateTime.UtcNow;
        await _repository.UpdateAsync(record, publishEvent: false);
    }
}
