namespace QuickMask.UI.Services;

public interface IFileDialogService
{
    Task<string?> PickImageAsync();
    Task<string?> PickSavePathAsync();
}
