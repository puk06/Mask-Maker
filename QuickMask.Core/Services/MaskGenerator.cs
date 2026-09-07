using System.Collections;
using SkiaSharp;

namespace QuickMask.Core.Services;

public static class MaskGenerator
{
    public static SKBitmap Generate(BitArray mask, int width, int height)
    {
        if (width < 0 || height < 0 || mask.Length != width * height)
            throw new ArgumentException("Mask dimensions do not match.", nameof(mask));

        var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque);
        var pixels = new SKColor[mask.Length];

        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = mask[i] ? SKColors.White : SKColors.Black;
        }

        bitmap.Pixels = pixels;

        return bitmap;
    }

    public static void Save(BitArray mask, int width, int height, string filePath, SKEncodedImageFormat format = SKEncodedImageFormat.Png, int quality = 100)
    {
        using var bitmap = Generate(mask, width, height);
        using var stream = File.Create(filePath);

        var result = bitmap.Encode(stream, format, quality);
        if (!result) throw new IOException($"Could not save mask image: {filePath}");
    }
}
