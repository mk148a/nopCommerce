using Newtonsoft.Json;

namespace Nop.Web.Models.JsonLD;

public record JsonLdAggregateRatingModel : JsonLdModel
{
    #region Properties

    [JsonProperty("@type")]
    public static string Type => "AggregateRating";

    [JsonProperty("ratingValue")]
    public decimal? RatingValue { get; set; }

    [JsonProperty("reviewCount")]
    public int ReviewCount { get; set; }

    [JsonProperty("bestRating")]
    public decimal? BestRating { get; set; }

    [JsonProperty("worstRating")]
    public decimal? WorstRating { get; set; }

    #endregion
}
