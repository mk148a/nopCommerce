using Newtonsoft.Json;

namespace Nop.Web.Models.JsonLD;

public record JsonLdAggregateRatingModel : JsonLdModel
{
    #region Properties

    [JsonProperty("@type")]
    public static string Type => "AggregateRating";

    [JsonProperty("ratingValue")]
    public decimal? RatingValue { get; set; }

    [JsonProperty("ratingCount")]
    public int RatingCount { get; set; }

    /// <summary>
    /// Backward-compatible source alias used by existing callers.  The public
    /// JSON-LD contract emits schema.org's ratingCount property.
    /// </summary>
    [JsonIgnore]
    public int ReviewCount
    {
        get => RatingCount;
        set => RatingCount = value;
    }

    [JsonProperty("bestRating")]
    public decimal? BestRating { get; set; }

    [JsonProperty("worstRating")]
    public decimal? WorstRating { get; set; }

    #endregion
}
