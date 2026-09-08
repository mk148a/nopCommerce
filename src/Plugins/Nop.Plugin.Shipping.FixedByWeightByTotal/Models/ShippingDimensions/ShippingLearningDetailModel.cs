namespace Nop.Plugin.Shipping.FixedByWeightByTotal.Models.ShippingDimensions;

public class ShippingLearningDetailModel
{
    public int ProductId { get; set; }
    public string ProductName { get; set; }
    public int OrderId { get; set; }
    public string CustomOrderNumber { get; set; }
    public string ShipmentNo { get; set; }
    public Guid? ShipmentUuid { get; set; }
    public int? PackageIndex { get; set; }

    public string ReceiverName { get; set; }
    public string ReceiverCountry { get; set; }
    public string ReceiverCity { get; set; }
    public DateTime? RequestDateTR { get; set; }

    public string CarrierTrackingNo { get; set; }
    public string NavlungoTrackingNo { get; set; }
    public string NavlungoDetailUrl { get; set; }
    public string CarrierTrackingUrl { get; set; }
    public string DocumentsLinks { get; set; }
    public string PackageImageLinks { get; set; }

    public string MatchDecision { get; set; }
    public int Score { get; set; }
    public int? SecondScore2 { get; set; }
    public int? ScoreGap { get; set; }

    public string AttributeHash { get; set; }
    public string AttributeDescription { get; set; }

    public decimal LengthCm { get; set; }
    public decimal WidthCm { get; set; }
    public decimal HeightCm { get; set; }
    public decimal WeightKg { get; set; }
    public decimal WeightGram { get; set; }
    public decimal ChargeableWeightKg { get; set; }
    public decimal VolumetricWeightDesi { get; set; }
    public decimal CalculatedDesi { get; set; }
}
