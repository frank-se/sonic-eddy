using System;
using System.Windows.Input;
using ReactiveUI;
using SonicEddy.Services.StreamingControl;

namespace SonicEddy.ViewModels.StreamingControlViewModels;

// One box in the Camera or Images row. Empty (IsEmpty = true) when there
// are fewer objects of that type in the active scene than the row's fixed
// number of boxes - the box just shows disabled rather than disappearing,
// so the row's layout stays stable as scenes change.
public sealed class ObjectSlotViewModel : ViewModelBase
{
    public ObjectSlotViewModel(int displayIndex)
    {
        DisplayIndex = displayIndex;
        IsEmpty = true;
        SelectCommand = ReactiveCommand.Create(() => { });
    }

    public ObjectSlotViewModel(int displayIndex, int flatIndex, SceneFileObject baseline,
        Action<int, SceneFileObject> select)
    {
        DisplayIndex = displayIndex;
        FlatIndex = flatIndex;
        Baseline = baseline;
        IsEmpty = false;
        SelectCommand = ReactiveCommand.Create(() => select(flatIndex, baseline));
    }

    public int DisplayIndex { get; }
    public int FlatIndex { get; }
    public SceneFileObject? Baseline { get; }
    public bool IsEmpty { get; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => this.RaiseAndSetIfChanged(ref _isSelected, value);
    }

    public ICommand SelectCommand { get; }
}
