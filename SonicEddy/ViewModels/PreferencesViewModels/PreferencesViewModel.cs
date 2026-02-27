using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Threading.Tasks;
using DynamicData;
using Fr.Wireplumber.Model.Objects;
using ReactiveUI;
using SonicEddy.Contracts.ApplicationPreferences;
using SonicEddy.Services.Preferences;
using SonicEddy.Services.Wireplumber;

namespace SonicEddy.ViewModels.PreferencesViewModels;

public class PreferencesViewModel : ViewModelBase
{
    private readonly CompositeDisposable _disposable = new();
    private readonly IWireplumberService _wireplumberService;
    private readonly IPreferenceService _preferenceService;

    private bool _changed = false;

    public PreferencesViewModel(IWireplumberService wireplumberService,
        IPreferenceService preferenceService)
    {
        _wireplumberService = wireplumberService;
        _preferenceService = preferenceService;

        var outputs = wireplumberService.GetCaptureNodes();

        Nodes.AddRange(outputs);

        this.WhenAnyValue(x => x.SelectedDefaultMasterOutput)
            .Subscribe(_ =>
            {
                _changed = true;
                UpdateButtonStates();
            })
            .DisposeWith(_disposable);

        _ = FillFromPreferences();
    }

    public ObservableCollection<Node> Nodes { get; } = [];

    public Node? SelectedDefaultMasterOutput
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsSaveEnabled
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = false;

    public async Task Save()
    {
        var preferences = new Preferences(SelectedDefaultMasterOutput?.Name);
        await _preferenceService.UpdateAndSave(preferences);
    }

    public bool IsRevertEnabled
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = false;

    public Task Revert()
    {
        _changed = false;
        return FillFromPreferences();
    }

    private async Task FillFromPreferences()
    {
        SelectedDefaultMasterOutput = null;

        if (_preferenceService.Preferences is null)
            await _preferenceService.Load();

        var preferences = _preferenceService.Preferences;

        if (preferences is null) return;

        var selectedNode = Nodes.FirstOrDefault(n =>
            n.Name == preferences.DefaultMasterOutputName);

        SelectedDefaultMasterOutput = selectedNode;
    }

    private void UpdateButtonStates()
    {
        IsSaveEnabled = _changed;
        IsRevertEnabled = _changed;
    }
}