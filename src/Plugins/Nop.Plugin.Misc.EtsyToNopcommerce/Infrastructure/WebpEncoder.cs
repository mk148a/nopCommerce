using System.IO;
using SkiaSharp;

namespace Nop.Plugin.Misc.EtsyToNopcommerce.Infrastructure;

internal static class WebpEncoder
{
    public static byte[] Encode(byte[] source, int quality = 90)
    {
        using var bitmap = SKBitmap.Decode(source)
            ?? throw new InvalidDataException("The Etsy review image could not be decoded.");
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Webp, quality)
            ?? throw new InvalidDataException("The Etsy review image could not be encoded as WebP.");

        return encoded.ToArray();
    }
}
