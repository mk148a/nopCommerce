using Newtonsoft.Json;

namespace Nop.Plugin.Widgets.FacebookPixel.Domain;

public class ConversionsEvent
{
    /// <summary>Test event code used only by the staging host.</summary>
    [JsonProperty(PropertyName = "test_event_code", NullValueHandling = NullValueHandling.Ignore)]
    public string TestEventCode { get; set; }
    /// <summary>
    /// Gets or sets an array of server event objects
    /// </summary>
    [JsonProperty(PropertyName = "data")]
    public List<ConversionsEventDatum> Data { get; set; }
}
