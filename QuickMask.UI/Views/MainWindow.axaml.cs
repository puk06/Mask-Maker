using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using QuickMask.UI.Localization;
using QuickMask.UI.ViewModels;
using SukiUI.Controls;

namespace QuickMask.UI.Views;

public partial class MainWindow : SukiWindow
{
    public MainWindow()
    {
        Localizer.Instance.LoadFromFolder(Path.Combine(AppContext.BaseDirectory, "locales"));
        Localizer.Instance.SetLanguage(0);

        InitializeComponent();
    }

    private void PreviewPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel || sender is not Control control)
            return;

        var point = e.GetPosition(control);
        var properties = e.GetCurrentPoint(control).Properties;
        if (properties.IsRightButtonPressed)
        {
            viewModel.SetBackgroundAt(point.X, point.Y, control.Bounds.Width, control.Bounds.Height);
            e.Handled = true;
            return;
        }

        if (properties.IsLeftButtonPressed)
        {
            var erase = (e.KeyModifiers & KeyModifiers.Control) != 0;
            viewModel.SelectAt(point.X, point.Y, control.Bounds.Width, control.Bounds.Height, erase);
        }
    }

    private void WindowDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Formats.Contains(DataFormat.File)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private async void WindowDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel || !e.DataTransfer.Formats.Contains(DataFormat.File))
            return;

        var path = e.DataTransfer.TryGetFiles()?.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path)) return;

        await viewModel.LoadImagePathAsync(path);

        e.Handled = true;
    }
}
