using System.Numerics;
using ErrorOr;
using QuickMask.Core.Localization;
using QuickMask.Core.Models;
using QuickMask.Core.Utils;
using SkiaSharp;

namespace QuickMask.Core.Services;

/// <summary>Finds four-connected objects in an image.</summary>
public sealed class ImageSelector(Image sourceImage, Image? guideImage = null) : IDisposable
{
    private const int InitialQueueCapacity = 64 * 1024;
    private const double FarDistance = 1e15;

    private Image? _sourceImage = sourceImage;
    private Image? _guideImage = guideImage;
    private Point _backgroundPoint;
    private int _uvExtraPixels;
    private PixelMask? _selectablePixels;
    private PixelMask? _visitedPixels;
    private PixelQueue _queue;
    private double[] _distances = [];
    private double[] _distanceColumn = [];
    private double[] _distanceOutput = [];
    private double[] _distanceIntersections = [];
    private int[] _distanceOrigins = [];
    private bool _initialized;
    private bool _disposed;

    /// <summary>Expands a selection made with a UV guide outward by this many pixels.</summary>
    public int UvExtraPixels
    {
        get => _uvExtraPixels;
        set => _uvExtraPixels = Math.Max(0, value);
    }

    public ErrorOr<Success> Initialize(Point backgroundPoint)
    {
        var image = GetImage();
        if (!ValidatePoint(backgroundPoint, image)) return Error.Failure(description: Loc.Error.ImageSelector.InvalidBackgroundPoint);

        if (_guideImage != null && (_guideImage.Width != image.Width || _guideImage.Height != image.Height))
            return Error.Failure(description: Loc.Error.ImageSelector.GuideImageSizeMismatch);

        // Selecting several objects with the same background must not rebuild this mask.
        if (_initialized && _backgroundPoint == backgroundPoint) return Result.Success;

        _backgroundPoint = backgroundPoint;
        _selectablePixels = BuildImageSelectablePixels(image, backgroundPoint);
        if (_guideImage != null) ApplyUvGuide(_selectablePixels, _guideImage, backgroundPoint);

        _visitedPixels = new PixelMask(image.PixelCount);
        _queue = new PixelQueue(Math.Min(image.PixelCount, InitialQueueCapacity), image.PixelCount);
        _initialized = true;

        return Result.Success;
    }

    private static PixelMask BuildImageSelectablePixels(Image image, Point backgroundPoint)
    {
        var pixels = image.AsSpan();
        var backgroundColor = pixels[PixelUtils.GetPixelIndex(backgroundPoint.X, backgroundPoint.Y, image.Width)];

        var result = new PixelMask(pixels.Length);
        var words = result.Words;

        for (var wordIndex = 0; wordIndex < words.Length; wordIndex++)
        {
            var offset = wordIndex << 6;
            var count = Math.Min(64, pixels.Length - offset);

            ulong word = 0;

            for (var bit = 0; bit < count; bit++)
            {
                if (!pixels[offset + bit].Equals(backgroundColor)) word |= 1UL << bit;
            }

            words[wordIndex] = word;
        }

        return result;
    }

    private static PixelMask BuildUvBackgroundPixels(Image image, Point backgroundPoint)
    {
        var pixels = image.AsSpan();
        var width = image.Width;
        var height = image.Height;

        var backgroundIndex = PixelUtils.GetPixelIndex(backgroundPoint.X, backgroundPoint.Y, width);
        var backgroundColor = pixels[backgroundIndex];

        var background = new PixelMask(pixels.Length);
        var queue = new PixelQueue(Math.Min(pixels.Length, InitialQueueCapacity), pixels.Length);

        queue.Enqueue(backgroundIndex);
        background[backgroundIndex] = true;

        while (queue.HasPending)
        {
            var index = queue.Dequeue();
            var x = index % width;
            var y = index / width;

            if (x > 0) TryVisitBackground(index - 1, pixels, backgroundColor, background, ref queue);
            if (x + 1 < width) TryVisitBackground(index + 1, pixels, backgroundColor, background, ref queue);
            if (y > 0) TryVisitBackground(index - width, pixels, backgroundColor, background, ref queue);
            if (y + 1 < height) TryVisitBackground(index + width, pixels, backgroundColor, background, ref queue);
        }

        return background;
    }

    private static void ApplyUvGuide(PixelMask selectablePixels, Image guideImage, Point backgroundPoint)
    {
        var backgroundPixels = BuildUvBackgroundPixels(guideImage, backgroundPoint);

        selectablePixels.AndNot(backgroundPixels);
    }

    private static void TryVisitBackground(int index, ReadOnlySpan<SKColor> pixels, SKColor backgroundColor, PixelMask visited, ref PixelQueue queue)
    {
        if (visited[index] || !pixels[index].Equals(backgroundColor)) return;

        visited[index] = true;
        queue.Enqueue(index);
    }

    public ErrorOr<SelectionArea> Select(Point point)
    {
        var image = GetImage();
        if (!_initialized || _selectablePixels == null || _visitedPixels == null)
        {
            return Error.Failure(description: Loc.Error.ImageSelector.NotProperlyInitialized);
        }

        if (!ValidatePoint(point, image)) return Error.Failure(description: Loc.Error.ImageSelector.InvalidSelectionPoint);

        var startIndex = PixelUtils.GetPixelIndex(point.X, point.Y, image.Width);
        if (!_selectablePixels[startIndex]) return Error.Failure(description: Loc.Error.ImageSelector.PointNotSelectable);

        return FloodFill(image, startIndex, _selectablePixels, _visitedPixels);
    }

    /// <summary>Detects every four-connected object, ordered from the top-left pixel.</summary>
    public ErrorOr<SelectionArea[]> DetectAllObjects()
    {
        var image = GetImage();
        if (!_initialized || _selectablePixels == null || _visitedPixels == null)
        {
            return Error.Failure(description: Loc.Error.ImageSelector.NotProperlyInitialized);
        }

        var unprocessed = new PixelMask(_selectablePixels.Length);
        unprocessed.Or(_selectablePixels);

        var results = new List<SelectionArea>();
        var searchIndex = 0;

        while (true)
        {
            var startIndex = FindNextSetBit(unprocessed, searchIndex);
            if (startIndex < 0) break;

            var selected = FloodFill(image, startIndex, _selectablePixels, _visitedPixels);
            results.Add(selected);

            unprocessed.AndNot(selected.Mask);
            searchIndex = startIndex + 1;
        }

        return results.ToArray();
    }

    private static int FindNextSetBit(PixelMask mask, int startIndex)
    {
        var words = mask.Words;
        var wordIndex = startIndex >> 6;
        var bit = startIndex & 63;

        while (wordIndex < words.Length)
        {
            var word = words[wordIndex] >> bit;
            if (word != 0) return (wordIndex << 6) + bit + BitOperations.TrailingZeroCount(word);

            wordIndex++;
            bit = 0;
        }

        return -1;
    }

    private SelectionArea FloodFill(Image image, int startIndex, PixelMask selectable, PixelMask visited)
    {
        var width = image.Width;
        var height = image.Height;

        var selected = new SelectionArea(width, height);
        var mask = selected.Mask;

        _queue.Reset();

        _queue.Enqueue(startIndex);
        visited[startIndex] = true;
        mask[startIndex] = true;

        var minX = startIndex % width;
        var maxX = minX;
        var minY = startIndex / width;
        var maxY = minY;

        while (_queue.HasPending)
        {
            var index = _queue.Dequeue();
            var x = index % width;
            var y = index / width;

            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;

            if (x > 0) TryVisitObject(index - 1, selectable, visited, mask, ref _queue);
            if (x + 1 < width) TryVisitObject(index + 1, selectable, visited, mask, ref _queue);
            if (y > 0) TryVisitObject(index - width, selectable, visited, mask, ref _queue);
            if (y + 1 < height) TryVisitObject(index + width, selectable, visited, mask, ref _queue);
        }

        var added = _guideImage != null && _uvExtraPixels > 0
            ? ExpandSelection(image, mask, _uvExtraPixels, ref minX, ref minY, ref maxX, ref maxY)
            : 0;

        selected.Bounds = new PixelBounds(minX, minY, maxX, maxY);
        selected.PixelCount = _queue.Count + added;

        ClearVisited(visited, _queue.VisitedIndices);

        return selected;
    }

    /// <summary>Grows the selection outward by <paramref name="radius"/> pixels with a round structuring element.</summary>
    private int ExpandSelection(Image image, PixelMask mask, int radius, ref int minX, ref int minY, ref int maxX, ref int maxY)
    {
        var width = image.Width;
        var height = image.Height;

        var left = Math.Max(0, minX - radius);
        var top = Math.Max(0, minY - radius);
        var right = Math.Min(width - 1, maxX + radius);
        var bottom = Math.Min(height - 1, maxY + radius);

        var regionWidth = right - left + 1;
        var regionHeight = bottom - top + 1;

        EnsureDistanceBuffers(regionWidth * regionHeight, Math.Max(regionWidth, regionHeight));

        var lineLength = Math.Max(regionWidth, regionHeight);

        var distances = _distances.AsSpan(0, regionWidth * regionHeight);
        var column = _distanceColumn.AsSpan(0, regionHeight);
        var output = _distanceOutput.AsSpan(0, lineLength);
        var origins = _distanceOrigins.AsSpan(0, lineLength);
        var intersections = _distanceIntersections.AsSpan(0, lineLength + 1);

        for (var y = 0; y < regionHeight; y++)
        {
            var rowOffset = y * regionWidth;
            var index = (top + y) * width + left;

            for (var x = 0; x < regionWidth; x++, index++)
                distances[rowOffset + x] = mask[index] ? 0 : FarDistance;
        }

        for (var y = 0; y < regionHeight; y++)
            TransformDistance(distances.Slice(y * regionWidth, regionWidth), output, origins, intersections);

        for (var x = 0; x < regionWidth; x++)
        {
            for (var y = 0; y < regionHeight; y++) column[y] = distances[y * regionWidth + x];

            TransformDistance(column, output, origins, intersections);

            for (var y = 0; y < regionHeight; y++) distances[y * regionWidth + x] = column[y];
        }

        var limit = (radius + 0.5) * (radius + 0.5);
        var added = 0;

        for (var y = 0; y < regionHeight; y++)
        {
            var rowOffset = y * regionWidth;
            var index = (top + y) * width + left;

            for (var x = 0; x < regionWidth; x++, index++)
            {
                if (mask[index] || distances[rowOffset + x] > limit) continue;

                mask[index] = true;
                added++;

                var pixelX = left + x;
                var pixelY = top + y;

                if (pixelX < minX) minX = pixelX;
                if (pixelX > maxX) maxX = pixelX;
                if (pixelY < minY) minY = pixelY;
                if (pixelY > maxY) maxY = pixelY;
            }
        }

        return added;
    }

    private void EnsureDistanceBuffers(int regionLength, int lineLength)
    {
        if (_distances.Length < regionLength) _distances = new double[regionLength];
        if (_distanceColumn.Length < lineLength) _distanceColumn = new double[lineLength];
        if (_distanceOutput.Length < lineLength) _distanceOutput = new double[lineLength];
        if (_distanceOrigins.Length < lineLength) _distanceOrigins = new int[lineLength];
        if (_distanceIntersections.Length <= lineLength) _distanceIntersections = new double[lineLength + 1];
    }

    /// <summary>One dimensional squared euclidean distance transform (Felzenszwalb and Huttenlocher).</summary>
    private static void TransformDistance(Span<double> values, Span<double> output, Span<int> origins, Span<double> intersections)
    {
        var length = values.Length;

        var last = 0;
        origins[0] = 0;
        intersections[0] = -FarDistance;
        intersections[1] = FarDistance;

        for (var q = 1; q < length; q++)
        {
            var border = Intersection(q, values[q], origins[last], values[origins[last]]);

            while (border <= intersections[last])
            {
                last--;
                border = Intersection(q, values[q], origins[last], values[origins[last]]);
            }

            last++;
            origins[last] = q;
            intersections[last] = border;
            intersections[last + 1] = FarDistance;
        }

        last = 0;

        for (var q = 0; q < length; q++)
        {
            while (intersections[last + 1] < q) last++;

            var offset = q - origins[last];
            output[q] = offset * offset + values[origins[last]];
        }

        output[..values.Length].CopyTo(values);
    }

    private static double Intersection(int q, double value, int origin, double originValue) => (value + q * q - (originValue + origin * origin)) / (2.0 * q - 2.0 * origin);

    private static void TryVisitObject(int index, PixelMask selectable, PixelMask visited, PixelMask selected, ref PixelQueue queue)
    {
        if (!selectable[index] || visited[index]) return;

        visited[index] = true;
        selected[index] = true;
        queue.Enqueue(index);
    }

    private static void ClearVisited(PixelMask visited, ReadOnlySpan<int> indices)
    {
        foreach (var index in indices) visited[index] = false;
    }

    private Image GetImage() => _sourceImage ?? throw new ObjectDisposedException(nameof(ImageSelector));

    private static bool ValidatePoint(Point point, Image image)
    {
        if (point.X < 0 || point.X >= image.Width || point.Y < 0 || point.Y >= image.Height)
            return false;
        return true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _sourceImage = null;
        _guideImage = null;
        _selectablePixels = null;
        _visitedPixels = null;
        _queue = default;
        _distances = [];
        _distanceColumn = [];
        _distanceOutput = [];
        _distanceIntersections = [];
        _distanceOrigins = [];
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private struct PixelQueue(int initialCapacity, int maxLength)
    {
        private int[] _buffer = new int[Math.Max(initialCapacity, 1)];
        private int _head;
        private int _tail;

        public readonly bool HasPending => _head < _tail;

        public readonly int Count => _tail;

        public readonly ReadOnlySpan<int> VisitedIndices => _buffer.AsSpan(0, _tail);

        public int Dequeue() => _buffer[_head++];

        public void Reset()
        {
            _head = 0;
            _tail = 0;
        }

        public void Enqueue(int index)
        {
            if (_tail == _buffer.Length) Grow();

            _buffer[_tail++] = index;
        }

        private void Grow()
        {
            var capacity = _tail < maxLength / 2 ? Math.Max(_tail * 2, 1024) : maxLength;

            Array.Resize(ref _buffer, capacity);
        }
    }
}
