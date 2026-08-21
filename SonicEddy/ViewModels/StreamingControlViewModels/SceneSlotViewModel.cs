using System;
using System.Windows.Input;
using Fr.Sonic.Compositor;
using ReactiveUI;

namespace SonicEddy.ViewModels.StreamingControlViewModels;

// One box in the scene picker row (up to kMaxScenes = 5). Empty when the
// compositor is currently running fewer scenes than that.
public sealed class SceneSlotViewModel : ViewModelBase
{
    public SceneSlotViewModel(int displayIndex)
    {
        DisplayIndex = displayIndex;
        IsEmpty = true;
        SelectCommand = ReactiveCommand.Create(() => { });
    }

    public SceneSlotViewModel(int displayIndex, int sceneIndex, CompositorSceneInfo info,
        Action<int, CompositorSceneInfo> select)
    {
        DisplayIndex = displayIndex;
        SceneIndex = sceneIndex;
        Info = info;
        IsEmpty = false;
        SelectCommand = ReactiveCommand.Create(() => select(sceneIndex, info));
    }

    public int DisplayIndex { get; }
    public int SceneIndex { get; }
    public CompositorSceneInfo? Info { get; }
    public bool IsEmpty { get; }
    public string Name => Info?.Name ?? string.Empty;

    private bool _isActive;
    public bool IsActive
    {
        get => _isActive;
        set => this.RaiseAndSetIfChanged(ref _isActive, value);
    }

    public ICommand SelectCommand { get; }
}
