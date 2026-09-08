using System.Collections;
using ErrorOr;
using QuickMask.Core.Localization;
using SkiaSharp;

namespace QuickMask.Core.Services;

public static class MaskGenerator
{
    public static ErrorOr<SKBitmap> Generate(BitArray mask, int width, int height)
    {
        SKBitmap? bitmap = null;

        try
        {
            if (width < 0 || height < 0 || mask.Length != width * height)
                return Error.Failure(description: Loc.Error.MaskGenerator.InvalidMaskDimensions);

            bitmap = new(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque);
            var pixels = new SKColor[mask.Length];

            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = mask[i] ? SKColors.White : SKColors.Black;
            }

            bitmap.Pixels = pixels;

            return bitmap;
        }
        catch
        {
            bitmap?.Dispose();
            return Error.Failure(description: Loc.Error.MaskGenerator.MaskGenerationFailed);
        }
    }

    public static ErrorOr<Success> Save(BitArray mask, int width, int height, string filePath, SKEncodedImageFormat format = SKEncodedImageFormat.Png, int quality = 100)
    {
        var generateResult = Generate(mask, width, height);
        if (generateResult.IsError) return Error.Failure(description: generateResult.FirstError.Description);

        var bitmap = generateResult.Value;

        using var stream = File.Create(filePath);

        var encodeResult = bitmap.Encode(stream, format, quality);
        if (!encodeResult) return Error.Failure(description: Loc.Error.MaskGenerator.MaskSaveFailed);

        return Result.Success;
    }
}
