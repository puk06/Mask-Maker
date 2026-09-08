using ErrorOr;
using QuickMask.Core.Services;
using SkiaSharp;

namespace QuickMask.Core.Models;

public class Image(string filePath) : IDisposable
{
    private readonly string _filePath = filePath;
    private SKColor[]? _pixels;
    private bool _disposed = false;
    private bool _loaded = false;

    public int Width { get; private set; }
    public int Height { get; private set; }
    public int PixelCount => Width * Height;

    public async Task<ErrorOr<Success>> Load()
    {
        try
        {
            if (_loaded) return Error.Failure(description: "Image is already loaded.");

            using (var bitmap = await ImageService.LoadImageAsync(_filePath))
            {
                Width = bitmap.Width;
                Height = bitmap.Height;

                var pixels = bitmap.Pixels;

                _pixels = new SKColor[pixels.Length];
                pixels.CopyTo(_pixels);
            }

            _loaded = true;

            return Result.Success;
        }
        catch (Exception ex)
        {
            return Error.Failure(description: $"Failed to load image: {ex.Message}");
        }
    }

    public ReadOnlySpan<SKColor> AsSpan()
    {
        if (_pixels == null) throw new InvalidOperationException("Image not loaded.");
        return _pixels.AsSpan();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            _pixels = null;
            _disposed = true;
        }
    }
}
