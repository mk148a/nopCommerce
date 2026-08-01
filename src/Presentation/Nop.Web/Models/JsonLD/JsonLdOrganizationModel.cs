using Newtonsoft.Json;

namespace Nop.Web.Models.JsonLD;

public record JsonLdOrganizationModel : JsonLdModel
{
    [JsonProperty("@id")]
    public string Id { get; set; }

    [JsonProperty("@type")]
    public static string Type => "Organization";
}
