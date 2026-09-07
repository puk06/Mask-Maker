using System.Collections;
using QuickMask.Core.Models;
using QuickMask.Core.Utils;

namespace QuickMask.Core.Services;

public sealed class QuickMask : IDisposable
{
    public Image? Image { get; private set; }
    public Image? UVImage { get; private set; }
    public string? LastError { get; private set; }
    public Point BackgroundPoint { get; set; }

    private ImageSelector? _imageSelector = null;

    public List<SelectionArea> Selections { get; } = [];

    public async Task LoadImage(string filePath)
    {
        var candidate = new Image(filePath);
        await candidate.Load();

        Selections.Clear();

        DisposeImageSelector(ref _imageSelector);

        Image?.Dispose();
        Image = candidate;

        UVImage?.Dispose();
        UVImage = candidate;
    }
    public async Task LoadUVGuideImage(string filePath)
    {
        var candidate = new Image(filePath);
        await candidate.Load();

        if (Image is not null && !HasMatchingDimensions(Image, candidate))
        {
            candidate.Dispose();
            LastError = "テクスチャ画像とUVガイド画像の解像度が一致していません。";
            return;
        }

        DisposeImageSelector(ref _imageSelector);

        UVImage?.Dispose();
        UVImage = candidate;
    }
    public void UnloadUVGuideImage()
    {
        DisposeImageSelector(ref _imageSelector);

        UVImage?.Dispose();
        UVImage = null;
    }
    private static void DisposeImageSelector(ref ImageSelector? selector)
    {
        selector?.Dispose();
        selector = null;
    }

    public bool Select(Point point, bool erase = false)
    {
        if (Image == null) return false;

        var selector = _imageSelector ??= new ImageSelector(Image, guideImage: UVImage);
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

    public SkiaSharp.SKBitmap GenerateMask() => MaskGenerator.Generate(MergeSelections(), Image?.Width ?? 0, Image?.Height ?? 0);

    public void SaveMask(string filePath, SkiaSharp.SKEncodedImageFormat format = SkiaSharp.SKEncodedImageFormat.Png, int quality = 100)
    {
        var width = Image?.Width ??  0;
        var height = Image?.Height ??  0;
        MaskGenerator.Save(MergeSelections(), width, height, filePath, format, quality);
    }

    public void Dispose()
    {
        DisposeImageSelector(ref _imageSelector);

        Image?.Dispose();
        Image = null;

        UVImage?.Dispose();
        UVImage = null;

        GC.SuppressFinalize(this);
    }
}
