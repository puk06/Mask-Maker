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

    public async Task Load()
    {
        if (_loaded) return;

        using (var bitmap = await ImageService.LoadImageAsync(_filePath))
        {
            Width = bitmap.Width;
            Height = bitmap.Height;

            var pixels = bitmap.Pixels;

            _pixels = new SKColor[pixels.Length];
            pixels.CopyTo(_pixels);
        }

        _loaded = true;
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
