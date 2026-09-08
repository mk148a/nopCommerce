namespace Nop.Plugin.Misc.StripeBnplCore.Models;

public sealed class StripeBnplPendingModel
{
    public string SessionId { get; set; }
    public Guid OrderGuid { get; set; }
    public string StatusUrl { get; set; }
    public string OrderDetailsUrl { get; set; }
}
