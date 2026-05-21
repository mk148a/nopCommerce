namespace Nop.Plugin.Shipping.FixedByWeightByTotal.Models.ShippingDimensions;

public class NavlungoShipmentLinkModel
{
    public int? NopShipmentId { get; set; }
    public int? OrderId { get; set; }
    public string CustomOrderNumber { get; set; }
    public string ShipmentNo { get; set; }
    public Guid? ShipmentUuid { get; set; }
    public string CarrierName { get; set; }
    public string CarrierTrackingNo { get; set; }
    public string NavlungoTrackingNo { get; set; }
    public string NavlungoDetailUrl { get; set; }
    public string CarrierTrackingUrl { get; set; }
    public string DocumentsLinks { get; set; }
    public string PackageImageLinks { get; set; }
    public DateTime? RequestDateTR { get; set; }
    public decimal? TotalActualWeightKg { get; set; }
    public decimal? TotalChargeableWeightKg { get; set; }
}
