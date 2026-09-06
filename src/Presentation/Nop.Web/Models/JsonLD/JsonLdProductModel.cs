using Newtonsoft.Json;

namespace Nop.Web.Models.JsonLD;

public record JsonLdProductModel : JsonLdModel
{
    #region Properties

    [JsonProperty("@context")]
    public static string Context => "https://schema.org";

    [JsonProperty("@type")]
    public static string Type => "Product";

    [JsonProperty("@id")]
    public string Id { get; set; }

    [JsonProperty("url")]
    public string Url { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("sku")]
    public string Sku { get; set; }

    [JsonProperty("gtin")]
    public string Gtin { get; set; }

    [JsonProperty("mpn")]
    public string Mpn { get; set; }

    [JsonProperty("description")]
    public string Description { get; set; }

    [JsonProperty("image")]
    public IList<string> Image { get; set; }

    [JsonProperty("brand")]
    public JsonLdBrandModel Brand { get; set; }

    [JsonProperty("category")]
    public string Category { get; set; }

    [JsonProperty("offers")]
    public JsonLdOfferModel Offer { get; set; }

    [JsonProperty("aggregateRating")]
    public JsonLdAggregateRatingModel AggregateRating { get; set; }

    [JsonProperty("review")]
    public IList<JsonLdReviewModel> Review { get; set; }

    [JsonProperty("hasVariant")]
    public IList<JsonLdProductModel> HasVariant { get; set; }

    [JsonIgnore]
    public bool SuppressOutput { get; set; }

    #endregion
}
