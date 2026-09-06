using Nop.Core.Configuration;

namespace Nop.Plugin.Misc.HoodWebpOptimizer;

/// <summary>Controls only the optimizer plugin; nopCommerce media settings remain untouched.</summary>
public sealed class HoodWebpSettings : ISettings
{
    public bool Enabled { get; set; } = true;
    public bool PreserveOriginals { get; set; } = true;
    public int MinimumSourceBytes { get; set; } = 32 * 1024;
    public int MinimumSavingsPercent { get; set; } = 8;
    public int MinimumQuality { get; set; } = 72;
    public int MaximumQuality { get; set; } = 90;
    public double MaximumPerceptualError { get; set; } = 0.0015d;
    public bool ProcessExistingPictures { get; set; }
    public int ExistingBatchSize { get; set; } = 20;
    /// <summary>Monotonic cursor prevents images with no safe WebP win from being revisited on every scheduled run.</summary>
    public int LegacyScanCursorPictureId { get; set; }
}
