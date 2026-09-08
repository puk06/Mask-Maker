using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using QuickMask.Core.Models;
using QuickMask.Core.Utils;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using SkiaSharp;

namespace QuickMask.UI.ViewModels;

public partial class MainViewModel : ReactiveObject, IDisposable
{
    public const string CurrentVersion = "v1.0.0";

    private readonly Services.IFileDialogService _dialogs;
    private readonly Core.Services.QuickMask _maker = new();
    private bool _disposed = false;

    [Reactive] public partial Bitmap? SourcePreview { get; private set; }
    public bool HasSourceImage => _maker.Image is not null;
    [Reactive] public partial Bitmap? MaskPreview { get; private set; }
    public ObservableCollection<SelectionAreaViewModel> SelectionAreas { get; } = [];
    [Reactive] public partial string Status { get; set; } = string.Empty;
    [Reactive] public partial string WindowTitle { get; set; } = string.Empty;
    [Reactive] public partial int BackgroundX { get; set; }
    [Reactive] public partial int BackgroundY { get; set; }

    public IReactiveCommand OpenImageCommand { get; }
    public IReactiveCommand OpenUvImageCommand { get; }
    public IReactiveCommand UnloadUvImageCommand { get; }
    public IReactiveCommand SaveMaskCommand { get; }
    public IReactiveCommand ClearSelectionsCommand { get; }

    public MainViewModel(Services.IFileDialogService dialogs)
    {
        _dialogs = dialogs;
        OpenImageCommand = ReactiveCommand.CreateFromTask(OpenImageAsync);
        OpenUvImageCommand = ReactiveCommand.CreateFromTask(OpenUvImageAsync);
        UnloadUvImageCommand = ReactiveCommand.Create(_maker.UnloadUVGuideImage);
        SaveMaskCommand = ReactiveCommand.CreateFromTask(SaveMaskAsync);
        ClearSelectionsCommand = ReactiveCommand.Create(ClearSelections);

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
            ShowStatus(result.FirstError.Description);
            return;
        }

        SelectionAreas.Clear();
        UpdateWindowTitle();
        SetSourcePreview(path);
        ShowStatus("画像をクリックしてオブジェクトを選択してください。");
        RefreshMaskPreview();
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
        if (result.IsError)
        {
            ShowStatus(result.FirstError.Description);
            return;
        }

        if (_maker.ImageLoaded)
        {
            ShowStatus("UVガイド画像が読み込まれました。ソース画像を開いて選択を開始してください。");
        }
        else
        {
            ShowStatus("UVガイド画像が読み込まれました。背景を設定するには、ソース画像を右クリックし、オブジェクトを選択するには左クリックしてください。");
        }
    }

    public void SelectAt(double x, double y, double displayWidth, double displayHeight, bool erase = false)
    {
        var point = GetImagePoint(x, y, displayWidth, displayHeight);
        if (point is null) return;

        _maker.BackgroundPoint = new Point(BackgroundX, BackgroundY);
        var result = _maker.Select(point.Value, erase: erase);
        if (result.IsError)
        {
            ShowStatus(result.FirstError.Description);
            return;
        }

        AddLatestSelection();
        RefreshMaskPreview();

        var areaType = erase ? "消去エリア" : "選択エリア";
        ShowStatus($"{areaType}を追加しました ({point.Value.X}, {point.Value.Y})。合計: {SelectionAreas.Count}個の選択エリア");
    }

    public void SetBackgroundAt(double x, double y, double displayWidth, double displayHeight)
    {
        var point = GetImagePoint(x, y, displayWidth, displayHeight);
        if (point == null) return;

        BackgroundX = point.Value.X;
        BackgroundY = point.Value.Y;
        _maker.BackgroundPoint = point.Value;
        ShowStatus($"背景ポイントが ({point.Value.X}, {point.Value.Y}) に設定されました。オブジェクトを選択するには左クリックしてください。");
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
            ShowStatus("画像を読み込んでから保存してください。");
            return;
        }

        var path = await _dialogs.PickSavePathAsync();
        if (path == null) return;

        var result = _maker.SaveMask(path);
        if (result.IsError)
        {
            ShowStatus(result.FirstError.Description);
            return;
        }

        ShowStatus($"マスク画像が保存されました: {Path.GetFileName(path)}");
    }

    private void ClearSelections()
    {
        _maker.ClearSelections();
        SelectionAreas.Clear();
        RefreshMaskPreview();
        ShowStatus("選択エリアがクリアされました。");
        UpdateWindowTitle();
    }

    private void AddLatestSelection()
    {
        var area = _maker.Selections[^1];
        SelectionAreas.Add(new(area, MoveSelection, RemoveSelection, RefreshSelectionState));
        UpdateSelectionIndexes();
        UpdateWindowTitle();
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
        RefreshMaskPreview();
        UpdateWindowTitle();
    }

    private void SetSourcePreview(string path)
    {
        SourcePreview?.Dispose();
        using var source = SKBitmap.Decode(path);
        SourcePreview = CreatePreviewBitmap(source);
    }

    private void RefreshMaskPreview()
    {
        MaskPreview?.Dispose();
        if (_maker.Image == null)
        {
            MaskPreview = null;
            return;
        }

        var result = _maker.GenerateMask();
        if (result.IsError)
        {
            MaskPreview = null;
            ShowStatus(result.FirstError.Description);
            return;
        }

        MaskPreview = CreatePreviewBitmap(result.Value);
        result.Value.Dispose();
    }

    private static Bitmap CreatePreviewBitmap(SKBitmap source)
    {
        const int maxSize = 512;
        var scale = Math.Min(1.0, maxSize / (double)Math.Max(source.Width, source.Height));
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));

        using var resized = source.Resize(new SKImageInfo(width, height), new(SKFilterMode.Linear, SKMipmapMode.Linear));
        using var image = SKImage.FromBitmap(resized ?? source);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = new MemoryStream(data.ToArray());
        return new Bitmap(stream);
    }

    private void UpdateWindowTitle()
    {
        var mask = _maker.MergeSelections();
        if (SelectionAreas.Count == 0)
            WindowTitle = $"QuickMask {CurrentVersion}";
        else
            WindowTitle = $"QuickMask {CurrentVersion} - {SelectionAreas.Count:N0}個の選択エリア (総選択ピクセル数: {BitArrayUtils.GetCount(mask, true):N0})";
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
            _maker.Dispose();
        }

        _disposed = true;
    }
}
