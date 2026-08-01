using System.Text.Json.Serialization;

namespace Nop.Web.Models.JsonLD;

/// <summary>
/// System.Text.Json payload for the public Product graph. The event model remains
/// Newtonsoft-compatible for existing consumers, while this DTO makes the emitted
/// product schema explicit and prevents serializer-specific property drift.
/// </summary>
public sealed class JsonLdProductPayload
{
    [JsonPropertyName("@context")]
    public string Context { get; init; } = "https://schema.org";

    [JsonPropertyName("@type")]
    public string Type { get; init; } = "Product";

    [JsonPropertyName("@id")]
    public string Id { get; init; }

    [JsonPropertyName("url")]
    public string Url { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; }

    [JsonPropertyName("sku")]
    public string Sku { get; init; }

    [JsonPropertyName("gtin")]
    public string Gtin { get; init; }

    [JsonPropertyName("mpn")]
    public string Mpn { get; init; }

    [JsonPropertyName("description")]
    public string Description { get; init; }

    [JsonPropertyName("image")]
    public IReadOnlyList<string> Image { get; init; }

    [JsonPropertyName("brand")]
    public JsonLdBrandPayload Brand { get; init; }

    [JsonPropertyName("category")]
    public string Category { get; init; }

    [JsonPropertyName("offers")]
    public JsonLdOfferPayload Offer { get; init; }

    [JsonPropertyName("aggregateRating")]
    public JsonLdAggregateRatingPayload AggregateRating { get; init; }

    [JsonPropertyName("review")]
    public IReadOnlyList<JsonLdReviewPayload> Review { get; init; }

    [JsonPropertyName("hasVariant")]
    public IReadOnlyList<JsonLdProductPayload> HasVariant { get; init; }

    public static JsonLdProductPayload From(JsonLdProductModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var images = model.Image?.Where(image => !string.IsNullOrWhiteSpace(image))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var reviews = model.Review?.Select(JsonLdReviewPayload.From).Where(review => review != null).ToArray();
        var variants = model.HasVariant?.Select(From).ToArray();

        return new JsonLdProductPayload
        {
            Id = model.Id,
            Url = model.Url,
            Name = model.Name,
            Sku = model.Sku,
            Gtin = model.Gtin,
            Mpn = model.Mpn,
            Description = model.Description,
            Image = images is { Length: > 0 } ? images : null,
            Brand = JsonLdBrandPayload.From(model.Brand),
            Category = model.Category,
            Offer = JsonLdOfferPayload.From(model.Offer),
            AggregateRating = JsonLdAggregateRatingPayload.From(model.AggregateRating),
            Review = reviews is { Length: > 0 } ? reviews : null,
            HasVariant = variants is { Length: > 0 } ? variants : null
        };
    }
}

public sealed class JsonLdBrandPayload
{
    [JsonPropertyName("@type")]
    public string Type { get; init; } = "Brand";

    [JsonPropertyName("name")]
    public string Name { get; init; }

    public static JsonLdBrandPayload From(JsonLdBrandModel model) => model == null ? null : new JsonLdBrandPayload { Name = model.Name };
}

public sealed class JsonLdOfferPayload
{
    [JsonPropertyName("@type")]
    public string Type { get; init; } = "Offer";

    [JsonPropertyName("@id")]
    public string Id { get; init; }

    [JsonPropertyName("url")]
    public string Url { get; init; }

    [JsonPropertyName("availability")]
    public string Availability { get; init; }

    [JsonPropertyName("price")]
    public decimal? Price { get; init; }

    [JsonPropertyName("priceCurrency")]
    public string PriceCurrency { get; init; }

    [JsonPropertyName("itemCondition")]
    public string ItemCondition { get; init; }

    [JsonPropertyName("seller")]
    public JsonLdOrganizationPayload Seller { get; init; }

    public static JsonLdOfferPayload From(JsonLdOfferModel model) => model == null ? null : new JsonLdOfferPayload
    {
        Id = model.Id,
        Url = model.Url,
        Availability = model.Availability,
        Price = model.Price,
        PriceCurrency = model.PriceCurrency,
        ItemCondition = model.ItemCondition,
        Seller = JsonLdOrganizationPayload.From(model.Seller)
    };
}

public sealed class JsonLdOrganizationPayload
{
    [JsonPropertyName("@id")]
    public string Id { get; init; }

    [JsonPropertyName("@type")]
    public string Type { get; init; } = "Organization";

    public static JsonLdOrganizationPayload From(JsonLdOrganizationModel model) => model == null ? null : new JsonLdOrganizationPayload { Id = model.Id };
}

public sealed class JsonLdAggregateRatingPayload
{
    [JsonPropertyName("@type")]
    public string Type { get; init; } = "AggregateRating";

    [JsonPropertyName("ratingValue")]
    public decimal? RatingValue { get; init; }

    [JsonPropertyName("reviewCount")]
    public int ReviewCount { get; init; }

    [JsonPropertyName("bestRating")]
    public decimal? BestRating { get; init; }

    [JsonPropertyName("worstRating")]
    public decimal? WorstRating { get; init; }

    public static JsonLdAggregateRatingPayload From(JsonLdAggregateRatingModel model) => model == null ? null : new JsonLdAggregateRatingPayload
    {
        RatingValue = model.RatingValue,
        ReviewCount = model.ReviewCount,
        BestRating = model.BestRating,
        WorstRating = model.WorstRating
    };
}

public sealed class JsonLdReviewPayload
{
    [JsonPropertyName("@type")]
    public string Type { get; init; } = "Review";

    [JsonPropertyName("author")]
    public JsonLdPersonPayload Author { get; init; }

    [JsonPropertyName("datePublished")]
    public string DatePublished { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; }

    [JsonPropertyName("reviewBody")]
    public string ReviewBody { get; init; }

    [JsonPropertyName("reviewRating")]
    public JsonLdRatingPayload ReviewRating { get; init; }

    public static JsonLdReviewPayload From(JsonLdReviewModel model) => model == null ? null : new JsonLdReviewPayload
    {
        Author = JsonLdPersonPayload.From(model.Author),
        DatePublished = model.DatePublished,
        Name = model.Name,
        ReviewBody = model.ReviewBody,
        ReviewRating = JsonLdRatingPayload.From(model.ReviewRating)
    };
}

public sealed class JsonLdPersonPayload
{
    [JsonPropertyName("@type")]
    public string Type { get; init; } = "Person";

    [JsonPropertyName("name")]
    public string Name { get; init; }

    public static JsonLdPersonPayload From(JsonLdPersonModel model) => model == null ? null : new JsonLdPersonPayload { Name = model.Name };
}

public sealed class JsonLdRatingPayload
{
    [JsonPropertyName("@type")]
    public string Type { get; init; } = "Rating";

    [JsonPropertyName("bestRating")]
    public decimal? BestRating { get; init; }

    [JsonPropertyName("ratingValue")]
    public int RatingValue { get; init; }

    [JsonPropertyName("worstRating")]
    public decimal? WorstRating { get; init; }

    public static JsonLdRatingPayload From(JsonLdRatingModel model) => model == null ? null : new JsonLdRatingPayload
    {
        BestRating = model.BestRating,
        RatingValue = model.RatingValue,
        WorstRating = model.WorstRating
    };
}
