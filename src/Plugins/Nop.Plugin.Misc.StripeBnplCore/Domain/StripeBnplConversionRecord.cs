using Nop.Core;

namespace Nop.Plugin.Misc.StripeBnplCore.Domain;

public class StripeBnplConversionRecord : BaseEntity
{
    public int OrderId { get; set; }
    public int ProviderId { get; set; }
    public string TransactionId { get; set; }
    public string PaymentType { get; set; }
    public decimal Value { get; set; }
    public string Currency { get; set; }
    public string Status { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? ConsumedOnUtc { get; set; }

    public BnplProvider Provider
    {
        get => (BnplProvider)ProviderId;
        set => ProviderId = (int)value;
    }
}
