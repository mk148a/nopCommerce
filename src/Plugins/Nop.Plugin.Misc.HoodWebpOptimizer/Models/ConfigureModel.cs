using Nop.Web.Framework.Models;

namespace Nop.Plugin.Misc.HoodWebpOptimizer.Models;

public sealed record class ConfigureModel : BaseNopModel
{
    public bool Enabled { get; set; }
    public bool PreserveOriginals { get; set; }
    public int MinimumSourceBytes { get; set; }
    public int MinimumSavingsPercent { get; set; }
    public int MinimumQuality { get; set; }
    public int MaximumQuality { get; set; }
    public double MaximumPerceptualError { get; set; }
    public bool ProcessExistingPictures { get; set; }
    public int ExistingBatchSize { get; set; }
    public int ProcessedInLastBatch { get; set; }
    public bool IsLocalPictureService { get; set; }
}
