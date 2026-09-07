namespace MaskMaker.UI.Services;

public interface IFileDialogService
{
    Task<string?> PickImageAsync();
    Task<string?> PickSavePathAsync();
}
