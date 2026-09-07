using System.Collections;
using MaskMaker.Core.Models;
using MaskMaker.Core.Utils;

namespace MaskMaker.Core.Services;

public sealed class MaskMaker : IDisposable
{
    public Image? Image { get; private set; }
    public Image? UVImage { get; private set; }
    public string? LastError { get; private set; }
    public Point BackgroundPoint { get; set; }

    private ImageSelector? _imageSelector = null;
    private ImageSelector? _uvImageSelector = null;

    public List<SelectionArea> Selections { get; } = [];

    public async Task LoadImage(string filePath)
    {
        LastError = null;
        var candidate = new Image(filePath);
        await candidate.Load();
        if (UVImage is not null && !HasMatchingDimensions(candidate, UVImage))
        {
            candidate.Dispose();
            LastError = "テクスチャ画像とUV画像の解像度が一致していません。";
            return;
        }

        Selections.Clear();
        _imageSelector?.Dispose();
        _imageSelector = null;
        Image?.Dispose();
        Image = candidate;
    }
    public async Task LoadUVImage(string filePath)
    {
        LastError = null;
        var candidate = new Image(filePath);
        await candidate.Load();
        if (Image is not null && !HasMatchingDimensions(Image, candidate))
        {
            candidate.Dispose();
            LastError = "テクスチャ画像とUV画像の解像度が一致していません。";
            return;
        }

        Selections.Clear();
        _uvImageSelector?.Dispose();
        _uvImageSelector = null;
        _imageSelector?.Dispose();
        _imageSelector = null;
        UVImage?.Dispose();
        UVImage = candidate;
    }

    public bool Select(Point point, bool fromUv = false, bool erase = false)
    {
        var image = fromUv ? UVImage : Image;
        if (image == null) return false;

        var selector = fromUv
            ? _uvImageSelector ??= new ImageSelector(image, true)
            : _imageSelector ??= new ImageSelector(image, guideImage: UVImage);
        selector.Initialize(BackgroundPoint);

        var selection = selector.Select(point);
        if (selection == null) return false;
        selection.IsErase = erase;
        Selections.Add(selection);
        return true;
    }

    public BitArray MergeSelections()
    {
        var result = new BitArray(Image?.PixelCount ?? UVImage?.PixelCount ?? 0);
        foreach (var selection in Selections)
        {
            if (!selection.IsEnabled) continue;
            BitArrayUtils.Merge(ref result, selection.Mask, selection.IsErase);
        }
        return result;
    }

    public void ClearSelections() => Selections.Clear();

    public void MoveSelection(int index, int offset)
    {
        var targetIndex = index + offset;
        if (index < 0 || index >= Selections.Count || targetIndex < 0 || targetIndex >= Selections.Count) return;

        (Selections[index], Selections[targetIndex]) = (Selections[targetIndex], Selections[index]);
    }

    private static bool HasMatchingDimensions(Image first, Image second) =>
        first.Width == second.Width && first.Height == second.Height;

    public SkiaSharp.SKBitmap GenerateMask() => MaskGenerator.Generate(MergeSelections(), Image?.Width ?? UVImage?.Width ?? 0, Image?.Height ?? UVImage?.Height ?? 0);

    public void SaveMask(string filePath,
        SkiaSharp.SKEncodedImageFormat format = SkiaSharp.SKEncodedImageFormat.Png,
        int quality = 100)
    {
        var width = Image?.Width ?? UVImage?.Width ?? 0;
        var height = Image?.Height ?? UVImage?.Height ?? 0;
        MaskGenerator.Save(MergeSelections(), width, height, filePath, format, quality);
    }

    public void Dispose()
    {
        _imageSelector?.Dispose();
        _uvImageSelector?.Dispose();
        Image?.Dispose();
        UVImage?.Dispose();
        _imageSelector = null;
        _uvImageSelector = null;
        Image = null;
        UVImage = null;
        GC.SuppressFinalize(this);
    }
}
