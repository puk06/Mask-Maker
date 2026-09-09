using ErrorOr;
using QuickMask.Core.Localization;
using QuickMask.Core.Models;
using SkiaSharp;

namespace QuickMask.Core.Services;

public sealed class QuickMask : IDisposable
{
    public Image? Image { get; private set; }
    public bool ImageLoaded => Image != null;

    public Image? UVImage { get; private set; }
    public bool UVImageLoaded => UVImage != null;

    public Point BackgroundPoint { get; set; }

    /// <summary>Expands selections made with a UV guide outward by this many pixels.</summary>
    public int UvExtraPixels { get; set; }

    private ImageSelector? _imageSelector = null;
    private PixelMask? _merged;

    public List<SelectionArea> Selections { get; } = [];

    public async Task<ErrorOr<Success>> LoadImage(string filePath)
    {
        var candidate = new Image(filePath);
        var result = await candidate.Load();
        if (result.IsError) return result;

        Selections.Clear();

        DisposeImageSelector(ref _imageSelector);

        Image?.Dispose();
        Image = candidate;

        UVImage?.Dispose();
        UVImage = null;

        return Result.Success;
    }
    public void UnloadImage()
    {
        DisposeImageSelector(ref _imageSelector);

        Image?.Dispose();
        Image = null;

        Selections.Clear();

        UVImage?.Dispose();
        UVImage = null;

        _merged = null;
    }

    public async Task<ErrorOr<Success>> LoadUVGuideImage(string filePath)
    {
        if (Image == null) return Error.Failure(description: Loc.Error.NoImageLoaded);

        var candidate = new Image(filePath);
        await candidate.Load();

        if (!HasMatchingDimensions(Image, candidate))
        {
            candidate.Dispose();
            return Error.Failure(description: Loc.Error.ImageSelector.DimensionMismatch);
        }

        DisposeImageSelector(ref _imageSelector);

        UVImage?.Dispose();
        UVImage = candidate;

        return Result.Success;
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

    public ErrorOr<Success> Select(Point point, bool erase = false)
    {
        if (Image == null) return Error.Failure(description: Loc.Error.NoImageLoaded);

        var selector = _imageSelector ??= new ImageSelector(Image, guideImage: UVImage);
        selector.UvExtraPixels = UvExtraPixels;

        var initializeResult = selector.Initialize(BackgroundPoint);
        if (initializeResult.IsError) return Error.Failure(description: initializeResult.FirstError.Description);

        var result = selector.Select(point);
        if (result.IsError) return Error.Failure(description: result.FirstError.Description);

        var selection = result.Value;

        selection.IsErase = erase;
        Selections.Add(selection);

        return Result.Success;
    }

    public ErrorOr<SelectionArea[]> SelectAllObjects(bool erase = false)
    {
        if (Image == null) return Error.Failure(description: Loc.Error.NoImageLoaded);

        var selector = _imageSelector ??= new ImageSelector(Image, guideImage: UVImage);
        selector.UvExtraPixels = UvExtraPixels;

        var initializeResult = selector.Initialize(BackgroundPoint);
        if (initializeResult.IsError) return Error.Failure(description: initializeResult.FirstError.Description);

        var result = selector.DetectAllObjects();
        if (result.IsError) return Error.Failure(description: result.FirstError.Description);

        foreach (var selection in result.Value)
        {
            selection.IsErase = erase;
            Selections.Add(selection);
        }

        return result;
    }

    public PixelMask MergeSelections()
    {
        var length = Image?.PixelCount ?? UVImage?.PixelCount ?? 0;
        var result = _merged;

        if (result == null || result.Length != length)
        {
            result = new PixelMask(length);
            _merged = result;
        }
        else result.Clear();

        foreach (var selection in Selections)
        {
            if (!selection.IsEnabled) continue;

            if (selection.IsErase) result.AndNot(selection.Mask);
            else result.Or(selection.Mask);
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

    private static bool HasMatchingDimensions(Image first, Image second) => first.Width == second.Width && first.Height == second.Height;

    public ErrorOr<SKBitmap> GenerateSelectionPreview(SKBitmap baseImage)
    {
        if (Image == null) return Error.Failure(description: Loc.Error.NoImageLoaded);

        return SelectionPreviewGenerator.Generate(Selections, Image.Width, Image.Height, baseImage);
    }

    public ErrorOr<SKBitmap> GenerateMaskPreview(PixelMask mask, int maxSize)
    {
        var width = Image?.Width ?? 0;
        var height = Image?.Height ?? 0;
        return MaskGenerator.GeneratePreview(mask, width, height, maxSize);
    }
    public ErrorOr<Success> SaveMask(string filePath, SKEncodedImageFormat format = SKEncodedImageFormat.Png, int quality = 100)
    {
        var width = Image?.Width ?? 0;
        var height = Image?.Height ?? 0;
        return MaskGenerator.Save(MergeSelections(), width, height, filePath, format, quality);
    }

    public void Dispose()
    {
        DisposeImageSelector(ref _imageSelector);

        Image?.Dispose();
        Image = null;

        UVImage?.Dispose();
        UVImage = null;

        _merged = null;

        GC.SuppressFinalize(this);
    }
}
