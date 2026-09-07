using SkiaSharp;

namespace QuickMask.Core.Services;

public static class ImageService
{
    public static Task<SKBitmap> LoadImageAsync(string filePath)
    {
        return Task.Run(() =>
        {
            using var stream = File.OpenRead(filePath);
            return SKBitmap.Decode(stream);
        });
    }
}
