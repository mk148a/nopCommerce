using Newtonsoft.Json;

namespace Nop.Web.Models.JsonLD;

public record JsonLdRatingModel : JsonLdModel
{
    #region Properties

    [JsonProperty("@type")]
    public static string Type => "Rating";

    [JsonProperty("bestRating")]
    public decimal? BestRating { get; set; }

    [JsonProperty("ratingValue")]
    public int RatingValue { get; set; }

    [JsonProperty("worstRating")]
    public decimal? WorstRating { get; set; }

    #endregion
}
