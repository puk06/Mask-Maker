using System.Collections;
using QuickMask.Core.Models;
using QuickMask.Core.Utils;
using SkiaSharp;

namespace QuickMask.Core.Services;

/// <summary>Finds four-connected objects in an image.</summary>
public sealed class ImageSelector(Image sourceImage, bool isUv = false, Image? guideImage = null) : IDisposable
{
    private Image? _sourceImage = sourceImage;
    private Image? _guideImage = guideImage;
    private readonly bool _isUv = isUv;
    private Point _backgroundPoint;
    private byte[]? _selectablePixels;
    private int[]? _visitMarks;
    private int[]? _queue;
    private int _visitToken;
    private bool _initialized;
    private bool _disposed;

    public void AddBackgroundPoint(Point point)
    {
        _backgroundPoint = point;
        _initialized = false;
    }

    public void Initialize() => Initialize(_backgroundPoint);

    public void Initialize(Point backgroundPoint)
    {
        var image = GetImage();
        ValidatePoint(backgroundPoint, image);

        if (_guideImage is not null && (_guideImage.Width != image.Width || _guideImage.Height != image.Height))
            throw new ArgumentException("The guide image dimensions must match the source image.");

        // Selecting several objects with the same background must not rebuild this mask.
        if (_initialized && _backgroundPoint == backgroundPoint) return;

        _backgroundPoint = backgroundPoint;
        _selectablePixels = _isUv ? BuildUvSelectablePixels(image, backgroundPoint) : BuildImageSelectablePixels(image, backgroundPoint);
        if (!_isUv && _guideImage is not null) ApplyUvGuide(_selectablePixels, _guideImage, backgroundPoint);
        _visitMarks = new int[image.PixelCount];
        _queue = new int[image.PixelCount];
        _visitToken = 0;
        _initialized = true;
    }

    private static byte[] BuildImageSelectablePixels(Image image, Point backgroundPoint)
    {
        var pixels = image.AsSpan();
        var width = image.Width;

        var backgroundColor = pixels[PixelUtils.GetPixelIndex(backgroundPoint.X, backgroundPoint.Y, width)];

        var result = new byte[pixels.Length];

        for (var i = 0; i < pixels.Length; i++)
        {
            result[i] = pixels[i].Equals(backgroundColor) ? (byte)0 : (byte)1;
        }

        return result;
    }

    private static byte[] BuildUvSelectablePixels(Image image, Point backgroundPoint)
    {
        var pixels = image.AsSpan();
        var width = image.Width;
        var height = image.Height;

        var backgroundIndex = PixelUtils.GetPixelIndex(backgroundPoint.X, backgroundPoint.Y, width);
        var backgroundColor = pixels[backgroundIndex];

        var background = new byte[pixels.Length];
        var queue = new int[pixels.Length];

        var head = 0;
        var tail = 1;

        queue[0] = backgroundIndex;
        background[queue[0]] = 1;

        while (head < tail)
        {
            var index = queue[head++];
            var x = index % width;
            var y = index / width;

            if (x > 0) TryVisitBackground(index - 1, pixels, backgroundColor, background, queue, ref tail);
            if (x + 1 < width) TryVisitBackground(index + 1, pixels, backgroundColor, background, queue, ref tail);
            if (y > 0) TryVisitBackground(index - width, pixels, backgroundColor, background, queue, ref tail);
            if (y + 1 < height) TryVisitBackground(index + width, pixels, backgroundColor, background, queue, ref tail);
        }

        for (var i = 0; i < background.Length; i++)
        {
            background[i] = background[i] == 0 ? (byte)1 : (byte)0;
        }

        return background;
    }

    private static void ApplyUvGuide(byte[] selectablePixels, Image guideImage, Point backgroundPoint)
    {
        var guidePixels = BuildUvSelectablePixels(guideImage, backgroundPoint);

        for (var i = 0; i < selectablePixels.Length; i++)
        {
            selectablePixels[i] &= guidePixels[i];
        }
    }

    private static void TryVisitBackground(int index, ReadOnlySpan<SKColor> pixels,
        SKColor backgroundColor, byte[] visited, int[] queue, ref int tail)
    {
        if (visited[index] != 0 || !pixels[index].Equals(backgroundColor)) return;

        visited[index] = 1;
        queue[tail++] = index;
    }

    public SelectionArea? Select(Point point)
    {
        var image = GetImage();
        if (!_initialized || _selectablePixels is null || _visitMarks is null || _queue is null) return null;
        if (point.X < 0 || point.X >= image.Width || point.Y < 0 || point.Y >= image.Height) return null;

        var startIndex = PixelUtils.GetPixelIndex(point.X, point.Y, image.Width);
        if (_selectablePixels[startIndex] == 0) return null;

        var token = NextVisitToken();
        var selected = new SelectionArea(image.Width, image.Height);

        var head = 0;
        var tail = 1;

        _queue[0] = startIndex;
        _visitMarks[startIndex] = token;
        selected.Mask[startIndex] = true;

        while (head < tail)
        {
            var index = _queue[head++];
            var x = index % image.Width;
            var y = index / image.Width;

            if (x > 0) TryVisitObject(index - 1, _selectablePixels, _visitMarks, token, selected.Mask, _queue, ref tail);
            if (x + 1 < image.Width) TryVisitObject(index + 1, _selectablePixels, _visitMarks, token, selected.Mask, _queue, ref tail);
            if (y > 0) TryVisitObject(index - image.Width, _selectablePixels, _visitMarks, token, selected.Mask, _queue, ref tail);
            if (y + 1 < image.Height) TryVisitObject(index + image.Width, _selectablePixels, _visitMarks, token, selected.Mask, _queue, ref tail);
        }

        return selected;
    }

    private static void TryVisitObject(int index, byte[] selectable, int[] marks, int token,
        BitArray selected, int[] queue, ref int tail)
    {
        if (selectable[index] == 0 || marks[index] == token) return;
        marks[index] = token;
        selected[index] = true;
        queue[tail++] = index;
    }

    private int NextVisitToken()
    {
        if (_visitToken == int.MaxValue)
        {
            Array.Clear(_visitMarks!);
            _visitToken = 0;
        }
        return ++_visitToken;
    }

    private Image GetImage() => _sourceImage ?? throw new ObjectDisposedException(nameof(ImageSelector));

    private static void ValidatePoint(Point point, Image image)
    {
        if (point.X < 0 || point.X >= image.Width || point.Y < 0 || point.Y >= image.Height)
            throw new ArgumentOutOfRangeException(nameof(point));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _sourceImage = null;
        _guideImage = null;
        _selectablePixels = null;
        _visitMarks = null;
        _queue = null;
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
