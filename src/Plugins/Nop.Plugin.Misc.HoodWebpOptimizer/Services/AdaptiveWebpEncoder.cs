using SkiaSharp;

namespace Nop.Plugin.Misc.HoodWebpOptimizer.Services;

/// <summary>
/// Chooses the smallest WebP candidate that remains below a conservative sampled pixel-error threshold.
/// This is intentionally a deterministic SSIM-like guard, not a claim of a full perceptual quality metric.
/// </summary>
public sealed class AdaptiveWebpEncoder : IAdaptiveWebpEncoder
{
    private static readonly HashSet<string> EligibleMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/jpg", "image/png", "image/bmp", "image/tiff"
    };

    public WebpEncodeResult TryEncode(byte[] source, string mimeType, HoodWebpSettings settings)
    {
        if (!settings.Enabled)
            return WebpEncodeResult.Skip("disabled");
        if (source is null || source.Length < settings.MinimumSourceBytes)
            return WebpEncodeResult.Skip("below-minimum-size");
        if (!EligibleMimeTypes.Contains(mimeType ?? string.Empty))
            return WebpEncodeResult.Skip("unsupported-or-already-webp");

        try
        {
            using var codec = SKCodec.Create(new SKMemoryStream(source));
            if (codec is null)
                return WebpEncodeResult.Skip("not-decodable");
            if (codec.FrameCount > 1)
                return WebpEncodeResult.Skip("animated-image");

            using var original = SKBitmap.Decode(source);
            if (original is null || original.Width < 2 || original.Height < 2)
                return WebpEncodeResult.Skip("invalid-dimensions");

            using var image = SKImage.FromBitmap(original);
            var best = WebpEncodeResult.Skip("no-size-quality-win");
            foreach (var quality in CandidateQualities(original, settings))
            {
                using var encoded = image.Encode(SKEncodedImageFormat.Webp, quality);
                var candidate = encoded?.ToArray();
                if (candidate is null || candidate.Length >= source.Length)
                    continue;

                var savings = (int)Math.Floor((source.Length - candidate.Length) * 100d / source.Length);
                if (savings < settings.MinimumSavingsPercent)
                    continue;

                using var decoded = SKBitmap.Decode(candidate);
                if (decoded is null || decoded.Width != original.Width || decoded.Height != original.Height)
                    continue;

                var error = SampledNormalizedMeanSquaredError(original, decoded);
                if (error > settings.MaximumPerceptualError)
                    continue;

                if (!best.Converted || candidate.Length < best.Binary.Length)
                    best = new WebpEncodeResult(true, candidate, null, quality, error, savings);
            }

            return best;
        }
        catch
        {
            // Failing closed preserves the original upload and leaves no partially encoded data.
            return WebpEncodeResult.Skip("encoder-failure");
        }
    }

    private static IEnumerable<int> CandidateQualities(SKBitmap bitmap, HoodWebpSettings settings)
    {
        var min = Math.Clamp(settings.MinimumQuality, 45, 95);
        var max = Math.Clamp(settings.MaximumQuality, min, 100);
        var texture = TextureScore(bitmap);
        var start = texture > 0.16d ? max : Math.Max(min, max - 5);
        for (var quality = start; quality >= min; quality -= 4)
            yield return quality;
        if ((start - min) % 4 != 0)
            yield return min;
    }

    private static double TextureScore(SKBitmap bitmap)
    {
        const int samples = 48;
        var total = 0d;
        var count = 0;
        for (var y = 0; y < samples; y++)
        {
            var py = y * (bitmap.Height - 1) / (samples - 1);
            for (var x = 0; x < samples - 1; x++)
            {
                var px = x * (bitmap.Width - 1) / (samples - 1);
                var a = Luma(bitmap.GetPixel(px, py));
                var b = Luma(bitmap.GetPixel(Math.Min(px + 1, bitmap.Width - 1), py));
                total += Math.Abs(a - b);
                count++;
            }
        }
        return count == 0 ? 0 : total / count / 255d;
    }

    private static double SampledNormalizedMeanSquaredError(SKBitmap original, SKBitmap candidate)
    {
        const int samples = 64;
        var sum = 0d;
        var count = 0;
        for (var y = 0; y < samples; y++)
        {
            var py = y * (original.Height - 1) / (samples - 1);
            for (var x = 0; x < samples; x++)
            {
                var px = x * (original.Width - 1) / (samples - 1);
                var a = original.GetPixel(px, py);
                var b = candidate.GetPixel(px, py);
                sum += Square(a.Red - b.Red) + Square(a.Green - b.Green) + Square(a.Blue - b.Blue) + Square(a.Alpha - b.Alpha);
                count += 4;
            }
        }
        return sum / (count * 255d * 255d);
    }

    private static double Luma(SKColor color) => 0.2126d * color.Red + 0.7152d * color.Green + 0.0722d * color.Blue;
    private static double Square(double value) => value * value;
}
