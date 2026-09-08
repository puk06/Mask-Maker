using QuickMask.Core.Localization;
using QuickMask.Core.Models;
using QuickMask.Core.Utils;
using QuickMask.UI.Localization;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.SourceGenerators;

namespace QuickMask.UI.ViewModels;

public partial class SelectionAreaViewModel : ReactiveObject
{
    [Reactive] public partial int Index { get; private set; }
    [Reactive] public partial string TypeText { get; private set; } = string.Empty;
    [Reactive] public partial string PixelCountText { get; set; } = string.Empty;
    [Reactive] public partial bool IsEnabled { get; set; }
    [Reactive] public partial bool IsErase { get; private set; }

    public IReactiveCommand MoveUpCommand { get; }
    public IReactiveCommand MoveDownCommand { get; }
    public IReactiveCommand ToggleEraseCommand { get; }
    public IReactiveCommand RemoveCommand { get; }

    public SelectionArea Area { get; }
    public int PixelCount => BitArrayUtils.GetCount(Area.Mask, true);

    private readonly Action<SelectionAreaViewModel, int> _move;
    private readonly Action<SelectionAreaViewModel> _remove;
    private readonly Action _changed;

    public SelectionAreaViewModel(SelectionArea area, Action<SelectionAreaViewModel, int> move, Action<SelectionAreaViewModel> remove, Action changed)
    {
        Area = area;
        IsEnabled = area.IsEnabled;
        IsErase = area.IsErase;

        _move = move;
        _remove = remove;
        _changed = changed;
        
        MoveUpCommand = ReactiveCommand.Create(() => _move(this, -1));
        MoveDownCommand = ReactiveCommand.Create(() => _move(this, 1));
        ToggleEraseCommand = ReactiveCommand.Create(ToggleErase);
        RemoveCommand = ReactiveCommand.Create(() => _remove(this));

        this.WhenAnyValue(x => x.IsEnabled)
            .Subscribe(_ =>
            {
                Area.IsEnabled = IsEnabled;
                _changed();
            });

        Localizer.Instance.LanguageChanged += UpdateTexts;
        UpdateTexts();
    }

    public void UpdateIndex(int index) => Index = index;

    private void ToggleErase()
    {
        IsErase = !IsErase;
        Area.IsErase = IsErase;

        UpdateTexts();
        _changed();
    }

    private void UpdateTexts()
    {
        TypeText = GetTypeText(IsErase);
        PixelCountText = Localizer.Instance.Get(Loc.SelectionArea.PixelCount, PixelCount.ToString("N0"));
    }

    private static string GetTypeText(bool isErase) => isErase ? Localizer.Instance[Loc.SelectionArea.AreaType.Eraser] : Localizer.Instance[Loc.SelectionArea.AreaType.Selection];
}
