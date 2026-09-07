using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using QuickMask.UI.ViewModels;
using SukiUI.Controls;

namespace QuickMask.UI.Views;

public partial class MainWindow : SukiWindow
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void SourcePointerPressed(object? sender, PointerPressedEventArgs e)
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

        if (viewModel.HasSourceImage)
            await viewModel.LoadUvImagePathAsync(path);
        else
            await viewModel.LoadImagePathAsync(path);

        e.Handled = true;
    }
}
