using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using Fr.Lv2.Model;
using ReactiveUI;

namespace SonicEddy.ViewModels.CreateModuleDialogViewModels;

public record ModuleType(string Name);

public class CreateModuleDialogViewModel : ViewModelBase, IDisposable
{
    private CompositeDisposable _disposables = new();

    public ObservableCollection<Lv2PluginNode> Nodes { get; } = [];

    public ObservableCollection<PluginDescription> Plugins { get; } = [];

    public ObservableCollection<ModuleType> SupportedModules { get; } =
    [
        new("Filter Chain"),
        new("Loopback")
    ];

    private ModuleType? _selectedModuleType;

    public ModuleType? SelectedModuleType
    {
        get => _selectedModuleType;
        set => this.RaiseAndSetIfChanged(ref _selectedModuleType, value);
    }

    private bool _isButtonEnabled;

    public bool IsButtonEnabled
    {
        get => _isButtonEnabled;
        set => this.RaiseAndSetIfChanged(ref _isButtonEnabled, value);
    }

    public bool DialogResult = false;

    public Interaction<Unit, Unit> Close { get; } = new();

    public async Task CancelAction()
    {
        DialogResult = false;
        await Close.Handle(Unit.Default);
    }

    public async Task AddModuleAction()
    {
        DialogResult = true;
        await Close.Handle(Unit.Default);
    }

    public CreateModuleDialogViewModel()
    {
        _ = Task.Run(LoadLv2Plugins);
    }

    private void LoadLv2Plugins() =>
        Plugins.AddRange(Fr.Lv2.Lv2.PluginDescriptions());

    public void AddPluginNode() => Nodes.Add(new(Plugins));

    public void Dispose() =>
        _disposables.Dispose();
}