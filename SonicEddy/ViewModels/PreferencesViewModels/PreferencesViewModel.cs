using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Threading.Tasks;
using DynamicData;
using Fr.Sonic.Model.Objects;
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

        var outputs = wireplumberService.GetCaptureNodes()
            .Where(n => string.IsNullOrEmpty(n.Pmx.Tag));

        Nodes.AddRange(outputs);

        this.WhenAnyValue(x => x.SelectedDefaultMasterOutput)
            .Subscribe(_ =>
            {
                _changed = true;
                UpdateButtonStates();
            })
            .DisposeWith(_disposable);

        this.WhenAnyValue(x => x.SelectedDefaultMonitorOutput)
            .Subscribe(_ =>
            {
                _changed = true;
                UpdateButtonStates();
            })
            .DisposeWith(_disposable);

        this.WhenAnyValue(x => x.MonitoringChannelEnabled)
            .Subscribe(_ =>
            {
                _changed = true;
                UpdateButtonStates();
            })
            .DisposeWith(_disposable);

        this.WhenAnyValue(x => x.NumberOfChannels)
            .Subscribe(_ =>
            {
                _changed = true;
                UpdateButtonStates();
            })
            .DisposeWith(_disposable);

        this.WhenAnyValue(x => x.NumberOfGroupChannels)
            .Subscribe(_ =>
            {
                _changed = true;
                UpdateButtonStates();
            })
            .DisposeWith(_disposable);

        this.WhenAnyValue(x => x.NumberOfReturnChannels)
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

    public Node? SelectedDefaultMonitorOutput
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool MonitoringChannelEnabled
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public int NumberOfChannels
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = 8;

    public int NumberOfGroupChannels
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = 4;

    public int NumberOfReturnChannels
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = 1;

    public bool IsSaveEnabled
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = false;

    public async Task Save()
    {
        var current = _preferenceService.Preferences ?? new Preferences();
        var preferences = current with
        {
            DefaultMasterOutputName = SelectedDefaultMasterOutput?.Name,
            NumberOfChannels = NumberOfChannels,
            NumberOfGroupChannels = NumberOfGroupChannels,
            NumberOfReturnChannels = NumberOfReturnChannels,
            DefaultMonitorOutputName = SelectedDefaultMonitorOutput?.Name,
            MonitoringChannelEnabled = MonitoringChannelEnabled
        };
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
        NumberOfChannels = preferences.NumberOfChannels;
        NumberOfGroupChannels = preferences.NumberOfGroupChannels;
        NumberOfReturnChannels = preferences.NumberOfReturnChannels;

        SelectedDefaultMonitorOutput = Nodes.FirstOrDefault(n =>
            n.Name == preferences.DefaultMonitorOutputName);
        MonitoringChannelEnabled = preferences.MonitoringChannelEnabled;
    }

    private void UpdateButtonStates()
    {
        IsSaveEnabled = _changed;
        IsRevertEnabled = _changed;
    }
}