using System.Runtime.InteropServices;
using ErrorOr;
using QuickMask.Core.Localization;
using QuickMask.Core.Models;
using SkiaSharp;

namespace QuickMask.Core.Services;

public static class MaskGenerator
{
    public static ErrorOr<SKBitmap> Generate(PixelMask mask, int width, int height)
    {
        SKBitmap? bitmap = null;

        try
        {
            if (width < 0 || height < 0 || mask.Length != width * height)
                return Error.Failure(description: Loc.Error.MaskGenerator.InvalidMaskDimensions);

            bitmap = new(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque);

            WriteMask(bitmap, mask, width, height);

            return bitmap;
        }
        catch
        {
            bitmap?.Dispose();
            return Error.Failure(description: Loc.Error.MaskGenerator.MaskGenerationFailed);
        }
    }

    public static ErrorOr<Success> Save(PixelMask mask, int width, int height, string filePath, SKEncodedImageFormat format = SKEncodedImageFormat.Png, int quality = 100)
    {
        var generateResult = Generate(mask, width, height);
        if (generateResult.IsError) return Error.Failure(description: generateResult.FirstError.Description);

        var bitmap = generateResult.Value;

        using var stream = File.Create(filePath);

        var encodeResult = bitmap.Encode(stream, format, quality);
        if (!encodeResult) return Error.Failure(description: Loc.Error.MaskGenerator.MaskSaveFailed);

        return Result.Success;
    }

    private static void WriteMask(SKBitmap bitmap, PixelMask mask, int width, int height)
    {
        if (width == 0 || height == 0) return;

        var pixels = MemoryMarshal.Cast<byte, SKColor>(bitmap.GetPixelSpan());
        var stride = bitmap.RowBytes / bitmap.BytesPerPixel;

        if (stride == width)
        {
            mask.WriteColors(pixels, 0, SKColors.White, SKColors.Black);
            return;
        }

        for (var y = 0; y < height; y++)
        {
            mask.WriteColors(pixels.Slice(y * stride, width), y * width, SKColors.White, SKColors.Black);
        }
    }
}
