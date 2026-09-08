using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PixelSize = Avalonia.PixelSize;
using Vector = Avalonia.Vector;
using QuickMask.Core.Localization;
using QuickMask.Core.Models;
using QuickMask.UI.Localization;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.SourceGenerators;
using SkiaSharp;

namespace QuickMask.UI.ViewModels;

public partial class MainViewModel : ReactiveObject, IDisposable
{
    public const string CurrentVersion = "1.0.0";
    private const int PreviewMaxSize = 512;

    private readonly Services.IFileDialogService _dialogs;
    private readonly Core.Services.QuickMask _maker = new();
    private SKBitmap? _sourcePreview;
    private bool _disposed = false;

    [Reactive] public partial Bitmap? SourcePreview { get; private set; }
    [Reactive] public partial Bitmap? MaskPreview { get; private set; }
    [Reactive] public partial double PreviewRatio { get; private set; } = 1.0;
    public ObservableCollection<SelectionAreaViewModel> SelectionAreas { get; } = [];

    [Reactive] public partial string WindowTitle { get; set; } = string.Empty;
    [Reactive] public partial int BackgroundX { get; set; }
    [Reactive] public partial int BackgroundY { get; set; }
    [Reactive] public partial string Status { get; set; } = string.Empty;
    [Reactive] public partial bool IsOverlayPreview { get; set; } = true;

    public IReactiveCommand OpenImageCommand { get; }
    public IReactiveCommand UnloadImageCommand { get; }
    public IReactiveCommand OpenUvImageCommand { get; }
    public IReactiveCommand UnloadUvImageCommand { get; }
    public IReactiveCommand SaveMaskCommand { get; }
    public IReactiveCommand ClearSelectionsCommand { get; }
    public IReactiveCommand SelectAllObjectsCommand { get; }

    public MainViewModel(Services.IFileDialogService dialogs)
    {
        _dialogs = dialogs;

        OpenImageCommand = ReactiveCommand.CreateFromTask(OpenImageAsync);
        UnloadImageCommand = ReactiveCommand.Create(UnloadImage);
        OpenUvImageCommand = ReactiveCommand.CreateFromTask(OpenUvImageAsync);
        UnloadUvImageCommand = ReactiveCommand.Create(UnloadUvImage);
        SaveMaskCommand = ReactiveCommand.CreateFromTask(SaveMaskAsync);
        ClearSelectionsCommand = ReactiveCommand.Create(ClearSelections);
        SelectAllObjectsCommand = ReactiveCommand.Create(SelectAllObjects);

        this.WhenAnyValue(x => x.IsOverlayPreview)
            .Subscribe(_ =>
            {
                RefreshSelectionState();
            });

        UpdateWindowTitle();
    }

    private async Task OpenImageAsync()
    {
        var path = await _dialogs.PickImageAsync();
        if (path == null) return;

        await LoadImagePathAsync(path);
    }
    public async Task LoadImagePathAsync(string path)
    {
        var result = await _maker.LoadImage(path);
        if (result.IsError)
        {
            ShowStatus(Localizer.Instance[result.FirstError.Description]);
            return;
        }

        SelectionAreas.Clear();
        SetSourcePreview(path);
        RefreshSelectionState();
    }
    private void UnloadImage()
    {
        _maker.UnloadImage();
        SelectionAreas.Clear();

        SourcePreview?.Dispose();
        SourcePreview = null;

        _sourcePreview?.Dispose();
        _sourcePreview = null;

        MaskPreview?.Dispose();
        MaskPreview = null;

        UpdateWindowTitle();
    }

    private async Task OpenUvImageAsync()
    {
        var path = await _dialogs.PickImageAsync();
        if (path == null) return;

        await LoadUvImagePathAsync(path);
    }
    public async Task LoadUvImagePathAsync(string path)
    {
        var result = await _maker.LoadUVGuideImage(path);
        if (result.IsError) ShowStatus(Localizer.Instance[result.FirstError.Description]);
        UpdateWindowTitle();
    }
    public void UnloadUvImage()
    {
        _maker.UnloadUVGuideImage();
        UpdateWindowTitle();
    }

    public void SelectAt(double x, double y, double displayWidth, double displayHeight, bool erase = false)
    {
        var point = GetImagePoint(x, y, displayWidth, displayHeight);
        if (point is null) return;

        _maker.BackgroundPoint = new Point(BackgroundX, BackgroundY);
        var result = _maker.Select(point.Value, erase: erase);
        if (result.IsError)
        {
            ShowStatus(Localizer.Instance[result.FirstError.Description]);
            return;
        }

        AddLatestSelection();
        RefreshSelectionState();

        var areaType = erase ? Localizer.Instance[Loc.SelectionArea.AreaType.Eraser] : Localizer.Instance[Loc.SelectionArea.AreaType.Selection];

        ShowStatus(Localizer.Instance.Get(Loc.Success.SelectionArea.Added, [areaType, point.Value.X.ToString(), point.Value.Y.ToString(), SelectionAreas.Count.ToString()]));
    }

    public void SetBackgroundAt(double x, double y, double displayWidth, double displayHeight)
    {
        var point = GetImagePoint(x, y, displayWidth, displayHeight);
        if (point == null) return;

        BackgroundX = point.Value.X;
        BackgroundY = point.Value.Y;
        _maker.BackgroundPoint = point.Value;

        ShowStatus(Localizer.Instance.Get(Loc.Success.BackgroundPoint.Set, [point.Value.X.ToString(), point.Value.Y.ToString()]));
    }

    private Point? GetImagePoint(double x, double y, double displayWidth, double displayHeight)
    {
        var image = _maker.Image;
        if (image == null || displayWidth <= 0 || displayHeight <= 0) return null;

        var scale = Math.Min(displayWidth / image.Width, displayHeight / image.Height);
        var contentWidth = image.Width * scale;
        var contentHeight = image.Height * scale;
        var imageX = (x - (displayWidth - contentWidth) / 2) / scale;
        var imageY = (y - (displayHeight - contentHeight) / 2) / scale;
        
        var point = new Point((int)Math.Floor(imageX), (int)Math.Floor(imageY));
        if (point.X < 0 || point.X >= image.Width || point.Y < 0 || point.Y >= image.Height)
            return null;

        return point;
    }

    private async Task SaveMaskAsync()
    {
        if (!_maker.ImageLoaded)
        {
            ShowStatus(Localizer.Instance[Loc.Error.NoImageLoaded]);
            return;
        }

        var path = await _dialogs.PickSavePathAsync();
        if (path == null) return;

        var result = _maker.SaveMask(path);
        if (result.IsError)
        {
            ShowStatus(Localizer.Instance[result.FirstError.Description]);
            return;
        }

        ShowStatus(Localizer.Instance.Get(Loc.Success.MaskSaved, [Path.GetFileName(path)]));
    }

    private void SelectAllObjects()
    {
        if (!_maker.ImageLoaded)
        {
            ShowStatus(Localizer.Instance[Loc.Error.NoImageLoaded]);
            return;
        }

        _maker.BackgroundPoint = new Point(BackgroundX, BackgroundY);

        var result = _maker.SelectAllObjects();
        if (result.IsError)
        {
            ShowStatus(Localizer.Instance[result.FirstError.Description]);
            return;
        }

        AddLatestSelections(result.Value.Length);
        RefreshSelectionState();

        ShowStatus(Localizer.Instance.Get(Loc.Success.SelectionArea.AddedMultiple, [result.Value.Length.ToString(), SelectionAreas.Count.ToString()]));
    }

    private void ClearSelections()
    {
        _maker.ClearSelections();
        SelectionAreas.Clear();

        RefreshSelectionState();
    }

    private void AddLatestSelection() => AddLatestSelections(1);

    private void AddLatestSelections(int count)
    {
        var start = _maker.Selections.Count - count;

        for (var i = 0; i < count; i++)
            SelectionAreas.Add(new(_maker.Selections[start + i], MoveSelection, RemoveSelection, RefreshSelectionState));

        UpdateSelectionIndexes();
    }

    private void MoveSelection(SelectionAreaViewModel item, int offset)
    {
        var index = SelectionAreas.IndexOf(item);
        var target = index + offset;
        if (index < 0 || target < 0 || target >= SelectionAreas.Count) return;

        _maker.MoveSelection(index, offset);
        SelectionAreas.Move(index, target);

        UpdateSelectionIndexes();
        RefreshSelectionState();
    }

    private void RemoveSelection(SelectionAreaViewModel item)
    {
        var index = SelectionAreas.IndexOf(item);
        if (index < 0) return;

        SelectionAreas.RemoveAt(index);
        _maker.Selections.RemoveAt(index);

        UpdateSelectionIndexes();
        RefreshSelectionState();
    }

    private void UpdateSelectionIndexes()
    {
        for (var i = 0; i < SelectionAreas.Count; i++)
            SelectionAreas[i].UpdateIndex(i + 1);
    }

    private void RefreshSelectionState()
    {
        var mask = _maker.MergeSelections();

        RefreshSelectionPreview(mask);
        UpdateWindowTitle(mask);
    }

    private void SetSourcePreview(string path)
    {
        SourcePreview?.Dispose();
        SourcePreview = null;

        _sourcePreview?.Dispose();
        _sourcePreview = null;

        using var source = SKBitmap.Decode(path);
        if (source == null) return;

        PreviewRatio = (double)source.Width / Math.Max(1, source.Height);

        var scale = Math.Min(1.0, PreviewMaxSize / (double)Math.Max(source.Width, source.Height));
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));

        using var resized = width == source.Width && height == source.Height
            ? null
            : source.Resize(new SKImageInfo(width, height), new(SKFilterMode.Linear, SKMipmapMode.Linear));

        _sourcePreview = (resized ?? source).Copy();
        if (_sourcePreview == null) return;

        SourcePreview = CreateBitmap(_sourcePreview);
    }

    private void RefreshSelectionPreview(PixelMask mask)
    {
        MaskPreview?.Dispose();
        MaskPreview = null;

        if (_maker.Image == null) return;
        if (IsOverlayPreview && _sourcePreview == null) return;

        var result = IsOverlayPreview
            ? _maker.GenerateSelectionPreview(_sourcePreview!)
            : _maker.GenerateMaskPreview(mask, PreviewMaxSize);

        if (result.IsError)
        {
            ShowStatus(Localizer.Instance[result.FirstError.Description]);
            return;
        }

        MaskPreview = CreateBitmap(result.Value);
        result.Value.Dispose();
    }

    private static unsafe Bitmap CreateBitmap(SKBitmap source)
    {
        var format = source.ColorType == SKColorType.Bgra8888 ? PixelFormat.Bgra8888 : PixelFormat.Rgba8888;
        var bitmap = new WriteableBitmap(new PixelSize(source.Width, source.Height), new Vector(96, 96), format, AlphaFormat.Premul);

        using var framebuffer = bitmap.Lock();

        var destination = new Span<byte>((void*)framebuffer.Address, framebuffer.RowBytes * source.Height);
        var pixels = source.GetPixelSpan();
        var rowBytes = Math.Min(source.RowBytes, framebuffer.RowBytes);

        for (var y = 0; y < source.Height; y++)
            pixels.Slice(y * source.RowBytes, rowBytes).CopyTo(destination.Slice(y * framebuffer.RowBytes, rowBytes));

        return bitmap;
    }

    private void UpdateWindowTitle() => UpdateWindowTitle(_maker.MergeSelections());

    private void UpdateWindowTitle(PixelMask mask)
    {
        var windowTitleValues = new List<string>
        {
            Localizer.Instance.Get(Loc.WindowTitle.Base, [CurrentVersion])
        };

        if (_maker.UVImageLoaded)
            windowTitleValues.Add(Localizer.Instance.Get(Loc.WindowTitle.UvGuideLoaded));
        else
            windowTitleValues.Add(Localizer.Instance.Get(Loc.WindowTitle.UvGuideNotLoaded));

        if (SelectionAreas.Count > 0)
            windowTitleValues.Add(Localizer.Instance.Get(Loc.WindowTitle.SelectionAreaCount, [SelectionAreas.Count.ToString("N0"), mask.PopCount().ToString("N0")]));

        WindowTitle = string.Join("  |  ", windowTitleValues);
    }

    private void ShowStatus(string message)
    {
        Status = message;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            SourcePreview?.Dispose();
            MaskPreview?.Dispose();
            _sourcePreview?.Dispose();
            _sourcePreview = null;
            _maker.Dispose();
        }

        _disposed = true;
    }
}
