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

    /// <summary>Renders the merged mask itself, each block averaged instead of generating a full resolution bitmap first.</summary>
    public static ErrorOr<SKBitmap> GeneratePreview(PixelMask mask, int width, int height, int maxSize)
    {
        SKBitmap? bitmap = null;

        try
        {
            if (width < 0 || height < 0 || mask.Length != width * height)
                return Error.Failure(description: Loc.Error.MaskGenerator.InvalidMaskDimensions);

            var scale = Math.Min(1.0, maxSize / (double)Math.Max(width, height));
            var previewWidth = Math.Max(1, (int)Math.Round(width * scale));
            var previewHeight = Math.Max(1, (int)Math.Round(height * scale));

            bitmap = new(previewWidth, previewHeight, SKColorType.Rgba8888, SKAlphaType.Opaque);

            WritePreview(bitmap, mask, width, height);

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

    private static void WritePreview(SKBitmap bitmap, PixelMask mask, int width, int height)
    {
        if (width == 0 || height == 0) return;

        var pixels = MemoryMarshal.Cast<byte, SKColor>(bitmap.GetPixelSpan());
        var stride = bitmap.RowBytes / bitmap.BytesPerPixel;
        var previewWidth = bitmap.Width;
        var previewHeight = bitmap.Height;

        for (var ty = 0; ty < previewHeight; ty++)
        {
            var y0 = ty * height / previewHeight;
            var y1 = Math.Max(y0 + 1, (ty + 1) * height / previewHeight);

            for (var tx = 0; tx < previewWidth; tx++)
            {
                var x0 = tx * width / previewWidth;
                var x1 = Math.Max(x0 + 1, (tx + 1) * width / previewWidth);

                var set = 0;
                for (var y = y0; y < y1; y++) set += mask.CountSetBits(y * width + x0, x1 - x0);

                var block = (y1 - y0) * (x1 - x0);
                var value = (byte)(block == 0 ? 0 : set * 255 / block);

                pixels[ty * stride + tx] = new SKColor(value, value, value);
            }
        }
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
