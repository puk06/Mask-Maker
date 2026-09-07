using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace MaskMaker.UI.Services;

public sealed class AvaloniaFileDialogService(Window owner) : IFileDialogService
{
    private static readonly FilePickerFileType Images = new("Images")
    {
        Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.webp"]
    };

    public async Task<string?> PickImageAsync()
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open image",
            AllowMultiple = false,
            FileTypeFilter = [Images]
        });
        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }

    public async Task<string?> PickSavePathAsync()
    {
        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save mask",
            SuggestedFileName = "mask.png",
            DefaultExtension = "png",
            FileTypeChoices = [new FilePickerFileType("PNG") { Patterns = ["*.png"] }]
        });
        return file?.TryGetLocalPath();
    }
}
