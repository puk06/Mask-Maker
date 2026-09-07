using MaskMaker.Core.Models;
using MaskMaker.Core.Utils;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.SourceGenerators;

namespace MaskMaker.UI.ViewModels;

public partial class SelectionAreaViewModel : ReactiveObject
{
    [Reactive] public partial int Index { get; private set; }
    [Reactive] public partial string TypeText { get; private set; }
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
        _move = move;
        _remove = remove;
        _changed = changed;
        IsEnabled = area.IsEnabled;
        IsErase = area.IsErase;
        TypeText = GetTypeText(IsErase);
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
    }


    public void UpdateIndex(int index) => Index = index;

    private void ToggleErase()
    {
        IsErase = !IsErase;
        Area.IsErase = IsErase;
        TypeText = GetTypeText(IsErase);
        _changed();
    }

    private static string GetTypeText(bool isErase) => isErase ? "消去エリア" : "選択エリア";
}
