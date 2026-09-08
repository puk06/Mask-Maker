using System.Runtime.InteropServices;
using ErrorOr;
using QuickMask.Core.Localization;
using QuickMask.Core.Models;
using SkiaSharp;

namespace QuickMask.Core.Services;

/// <summary>Draws the selection over a preview sized copy of the source image: stripes outside, a frame inside.</summary>
public static class SelectionPreviewGenerator
{
    private const int FrameWidth = 2;
    private const int StripeInterval = 7;

    private static readonly SKColor Highlight = new(255, 60, 60);

    public static ErrorOr<SKBitmap> Generate(IReadOnlyList<SelectionArea> selections, int sourceWidth, int sourceHeight, SKBitmap baseImage)
    {
        SKBitmap? preview = null;

        try
        {
            if (sourceWidth <= 0 || sourceHeight <= 0 || baseImage.Width <= 0 || baseImage.Height <= 0)
                return Error.Failure(description: Loc.Error.MaskGenerator.InvalidMaskDimensions);

            preview = baseImage.Copy();
            if (preview == null) return Error.Failure(description: Loc.Error.MaskGenerator.MaskGenerationFailed);

            var destination = MemoryMarshal.Cast<byte, SKColor>(preview.GetPixelSpan());
            var selected = BuildPreviewMask(selections, sourceWidth, sourceHeight, preview.Width, preview.Height);

            DrawOverlay(selected, preview.Width, preview.Height, destination);

            return preview;
        }
        catch
        {
            preview?.Dispose();
            return Error.Failure(description: Loc.Error.MaskGenerator.MaskGenerationFailed);
        }
    }

    private static PixelMask BuildPreviewMask(IReadOnlyList<SelectionArea> selections, int sourceWidth, int sourceHeight, int previewWidth, int previewHeight)
    {
        var selected = new PixelMask(previewWidth * previewHeight);
        var sourceRows = new int[previewHeight];
        var sourceColumns = new int[previewWidth];

        for (var y = 0; y < previewHeight; y++)
            sourceRows[y] = Math.Min(sourceHeight - 1, y * sourceHeight / previewHeight) * sourceWidth;

        for (var x = 0; x < previewWidth; x++)
            sourceColumns[x] = Math.Min(sourceWidth - 1, x * sourceWidth / previewWidth);

        // Walking backwards, the last selection covering a pixel wins, so each pixel is resolved at most once.
        var resolved = new bool[selected.Length];

        for (var i = selections.Count - 1; i >= 0; i--)
        {
            var selection = selections[i];
            if (!selection.IsEnabled) continue;

            var mask = selection.Mask;
            var value = !selection.IsErase;
            var bounds = selection.Bounds;

            var lastX = Math.Min(previewWidth - 1, (bounds.MaxX * previewWidth / sourceWidth) + 1);
            var lastY = Math.Min(previewHeight - 1, (bounds.MaxY * previewHeight / sourceHeight) + 1);

            for (var y = Math.Max(0, (bounds.MinY * previewHeight / sourceHeight) - 1); y <= lastY; y++)
            {
                var sourceRow = sourceRows[y];
                var row = y * previewWidth;

                for (var x = Math.Max(0, (bounds.MinX * previewWidth / sourceWidth) - 1); x <= lastX; x++)
                {
                    var index = row + x;
                    if (resolved[index] || !mask[sourceRow + sourceColumns[x]]) continue;

                    selected[index] = value;
                    resolved[index] = true;
                }
            }
        }

        return selected;
    }

    private static void DrawOverlay(PixelMask selected, int previewWidth, int previewHeight, Span<SKColor> destination)
    {
        for (var y = 0; y < previewHeight; y++)
        {
            var row = y * previewWidth;

            for (var x = 0; x < previewWidth; x++)
            {
                var index = row + x;

                if (!selected[index])
                {
                    if ((x + y) % StripeInterval == 0) destination[index] = Highlight;
                    continue;
                }

                if (IsFrame(selected, x, y, previewWidth, previewHeight)) destination[index] = Highlight;
            }
        }
    }

    private static bool IsFrame(PixelMask selected, int x, int y, int previewWidth, int previewHeight)
    {
        var index = y * previewWidth + x;

        return x < FrameWidth || x >= previewWidth - FrameWidth || y < FrameWidth || y >= previewHeight - FrameWidth
            || !selected[index - FrameWidth]
            || !selected[index + FrameWidth]
            || !selected[index - (previewWidth * FrameWidth)]
            || !selected[index + (previewWidth * FrameWidth)];
    }
}
