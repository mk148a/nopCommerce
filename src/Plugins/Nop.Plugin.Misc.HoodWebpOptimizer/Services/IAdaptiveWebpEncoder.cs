namespace Nop.Plugin.Misc.HoodWebpOptimizer.Services;

public interface IAdaptiveWebpEncoder
{
    WebpEncodeResult TryEncode(byte[] source, string mimeType, HoodWebpSettings settings);
}

public sealed record WebpEncodeResult(bool Converted, byte[] Binary, string SkipReason, int Quality, double PerceptualError, int SavingsPercent)
{
    public static WebpEncodeResult Skip(string reason) => new(false, null, reason, 0, 0, 0);
}
