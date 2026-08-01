using Newtonsoft.Json;

namespace Nop.Web.Models.JsonLD;

public record JsonLdOfferModel : JsonLdModel
{
    #region Properties

    [JsonProperty("@type")]
    public static string Type => "Offer";

    [JsonProperty("@id")]
    public string Id { get; set; }

    [JsonProperty("url")]
    public string Url { get; set; }

    [JsonProperty("availability")]
    public string Availability { get; set; }

    [JsonProperty("price")]
    public decimal? Price { get; set; }

    [JsonProperty("priceCurrency")]
    public string PriceCurrency { get; set; }

    [JsonProperty("itemCondition")]
    public string ItemCondition { get; set; }

    [JsonProperty("seller")]
    public JsonLdOrganizationModel Seller { get; set; }

    #endregion
}
