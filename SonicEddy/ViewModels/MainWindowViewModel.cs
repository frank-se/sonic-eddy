using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Fr.Sonic;
using Fr.Sonic.PInvoke;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using SonicEddy.Services.AppData;
using SonicEddy.Services.DrumMixer;
using SonicEddy.Services.ExternalEffects;
using SonicEddy.Services.Midi;
using SonicEddy.Services.MixerServiceV2;
using SonicEddy.Services.MixerPersistence;
using SonicEddy.Services.TraktorZ1;
using SonicEddy.Tools;
using SonicEddy.Services.MixerViewModels;
using SonicEddy.Services.Preferences;
using SonicEddy.Services.RecordingPickUp;
using SonicEddy.Services.JackInputPorts;
using SonicEddy.Services.VirtualInputs;
using SonicEddy.Services.VirtualOutputs;
using SonicEddy.Services.Wireplumber;
using SonicEddy.ViewModels.FilterGraphManagerViewModels;
using SonicEddy.ViewModels.DrumMixerViewModels;
using SonicEddy.ViewModels.ExternalEffectsViewModels;
using SonicEddy.ViewModels.MetadataViewModels;
using SonicEddy.ViewModels.MidiParameterChangeMonitorViewModels;
using SonicEddy.ViewModels.MidiRouterViewModels;
using SonicEddy.ViewModels.MixerViewModelsV2;
using SonicEddy.ViewModels.MixerPersistenceViewModels;
using SonicEddy.ViewModels.ModuleManagerViewModels;
using SonicEddy.ViewModels.ObjectBrowserViewModels;
using SonicEddy.ViewModels.PreferencesViewModels;
using SonicEddy.ViewModels.MidiSyncViewModels;
using SonicEddy.ViewModels.ClickSyncViewModels;
using SonicEddy.ViewModels.SynchronizationViewModels;
using SonicEddy.ViewModels.MonitoringViewModels;
using SonicEddy.ViewModels.TransportViewModels;
using SonicEddy.ViewModels.GlobalMasterViewModels;
using SonicEddy.ViewModels.MixerOverviewViewModels;
using SonicEddy.ViewModels.RecordingPickUpViewModels;
using SonicEddy.ViewModels.JackInputPortsViewModels;
using SonicEddy.ViewModels.VirtualInputsViewModels;
using SonicEddy.ViewModels.VirtualOutputsViewModels;
using SonicEddy.Views.FilterGraphManagerViews;
using SonicEddy.Views.DrumMixerViews;
using SonicEddy.Views.ExternalEffectsViews;
using SonicEddy.Views.GlobalMasterViews;
using SonicEddy.Views.MicChannelViews;
using SonicEddy.Views.MetadataViews;
using SonicEddy.Views.MixerOverviewViews;
using SonicEddy.Views.MidiParameterChangeMonitorView;
using SonicEddy.Views.MidiRouterViews;
using SonicEddy.Views.ModuleManagerViews;
using SonicEddy.Views.ObjectBrowserViews;
using SonicEddy.Views.PreferencesViews;
using SonicEddy.Views.MidiSyncViews;
using SonicEddy.Views.ClickSyncViews;
using SonicEddy.Views.MixerPersistenceViews;
using SonicEddy.Views.SynchronizationViews;
using SonicEddy.Views.MonitoringViews;
using SonicEddy.Views.RecordingPickUpViews;
using SonicEddy.Views.JackInputPortsViews;
using SonicEddy.Views.VirtualInputsViews;
using SonicEddy.Views.VirtualOutputsViews;
using Splat;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace SonicEddy.ViewModels;

public class MainWindowViewModel : ViewModelBase, IDisposable
{
    public MainWindowViewModel(IMidiControllerService midiControllerService,
        ITraktorZ1Service traktorZ1Service,
        ILogger<MainWindowViewModel> logger, ILoggerFactory loggerFactory)
    {
        _midiControllerService = midiControllerService;
        _traktorZ1Service = traktorZ1Service;
        _logger = logger;
        _loggerFactory = loggerFactory;

        var preferenceService = Locator.Current.GetService<IPreferenceService>();
        if (preferenceService is not null)
        {
            IsMonitoringEnabled =
                preferenceService.Preferences?.MonitoringChannelEnabled ?? false;
            preferenceService.Changed += () =>
            {
                IsMonitoringEnabled =
                    preferenceService.Preferences?.MonitoringChannelEnabled ??
                    false;
            };
        }

        _drumMixerService = Locator.Current.GetService<IDrumMixerService>();
        if (_drumMixerService is not null)
        {
            IsDrumMixerEnabled = _drumMixerService.IsAvailable;
            _drumMixerService.Changed += OnDrumMixerChanged;
        }

        _midiControllerService.LayerChanged += OnLayerSelected;
        _midiControllerService.FilterParamsSectionMovedRight +=
            OnMoveFilterParamsPageRight;

        _midiControllerService.FilterParamsSectionMovedLeft +=
            OnMoveFilterParamsPageLeft;

        _midiControllerService.SelectedChannelChanged +=
            OnSelectedChannelChanged;

        _midiControllerService.DialSelectionModeChanged +=
            OnDialSelectionModeChanged;

        _midiControllerService.SelectedFilterParamsSectionChanged +=
            OnFilterMidiControlSectionIdChanged;

        _traktorZ1Service.FilterSectionChanged += OnTraktorZ1FilterSectionChanged;

        _ = NavigateToMixerV2ViewLayerA();
    }

    private const int NumberOfGroupChannels = 8;
    private const int NumberOfGroupChannelsPerLayer = NumberOfGroupChannels / 2;
    private const int NumberOfChannels = 16;
    private const int NumberOfChannelsPerLayer = NumberOfChannels / 2;

    private Window? _globalMasterWindow;
    private Window? _micChannelWindow;
    private Window? _mixerOverviewWindow;
    private Window? _drumMixerWindow;
    private Window? _externalEffectsWindow;
    private ExternalEffectsViewModel? _externalEffectsViewModel;
    private DrumMixerViewModel? _drumMixerViewModel;
    private Window? _monitoringWindow;
    private Window? _midiParameterMonitorWindow;
    private Window? _objectBrowserWindow;
    private Window? _metadataManagerWindow;
    private Window? _moduleManagerWindow;
    private Window? _filterGraphWindow;
    private Window? _virtualInputsWindow;
    private Window? _virtualOutputsWindow;
    private Window? _jackInputPortsWindow;
    private Window? _recordingPickUpsWindow;
    private Window? _preferencesWindow;
    private Window? _synchronizationWindow;
    private Window? _midiSyncWindow;
    private Window? _clickSyncWindow;
    private Window? _midiRouterWindow;
    private readonly SemaphoreSlim _layerInitializationLock = new(1, 1);
    private GlobalMasterViewModel? _globalMasterViewModel;
    private MicChannelsViewModel? _micChannelsViewModel;
    private MixerOverviewViewModel? _mixerOverviewViewModel;
    private Guid _currentMixerId;
    private string _currentMixerName = string.Empty;

    public bool IsInitialized
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public TransportToolbarViewModel Transport { get; } = new();

    public bool IsMonitoringEnabled
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsDrumMixerEnabled
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsMicChannelEnabled
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public MixerLayerViewModel? LayerAViewModel
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public MixerLayerViewModel? LayerBViewModel
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool MixerViewV2LayerAViewSelected
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool MixerViewV2LayerBViewSelected
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    private readonly IMidiControllerService _midiControllerService;
    private readonly ITraktorZ1Service _traktorZ1Service;
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IDrumMixerService? _drumMixerService;

    private void OnDrumMixerChanged()
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            IsDrumMixerEnabled = _drumMixerService?.IsAvailable ?? false);
    }

    public void ShowDrumMixerWindow()
    {
        if (_drumMixerService is not { IsAvailable: true })
            return;

        if (_drumMixerWindow is not null)
        {
            _drumMixerWindow.Activate();
            return;
        }

        _drumMixerViewModel = new DrumMixerViewModel(_drumMixerService);
        _drumMixerWindow = new DrumMixerWindow
        {
            DataContext = _drumMixerViewModel
        };
        _drumMixerWindow.Closed += (_, _) =>
        {
            _drumMixerViewModel?.Dispose();
            _drumMixerViewModel = null;
            _drumMixerWindow = null;
        };
        _drumMixerWindow.Show();
    }

    public void ShowExternalEffectsWindow()
    {
        if (_externalEffectsWindow is not null)
        {
            _externalEffectsWindow.Activate();
            return;
        }

        var service = Locator.Current.GetService<IExternalEffectService>();
        var wireplumber = Locator.Current.GetService<IWireplumberService>();
        if (service is null || wireplumber is null) return;
        _externalEffectsViewModel = new(service, wireplumber);
        _externalEffectsWindow = new ExternalEffectsWindow
        {
            DataContext = _externalEffectsViewModel
        };
        _externalEffectsWindow.Closed += (_, _) =>
        {
            _externalEffectsViewModel?.Dispose();
            _externalEffectsViewModel = null;
            _externalEffectsWindow = null;
        };
        _externalEffectsWindow.Show();
    }

    private void OnTraktorZ1FilterSectionChanged(TraktorZ1Side side, int sectionIndex)
    {
        if (side == TraktorZ1Side.Left)
            LayerAViewModel?.SetMasterChannelFilterMidiControlSectionId(
                (ulong)sectionIndex);
        else
            LayerBViewModel?.SetMasterChannelFilterMidiControlSectionId(
                (ulong)sectionIndex);
    }

    private void OnFilterMidiControlSectionIdChanged(
        FilterParamsSectionSelectEventArgs eventArgs)
    {
        if (_logger.IsEnabled(LogLevel.Trace))
            _logger.LogTrace(
                "OnFilterMidiControlSectionIdChanged, event args={eventArgs}",
                eventArgs);

        if (eventArgs.ChannelType == ChannelType.Channel)
        {
            var channelId = eventArgs.ChannelId;
            var layerId = 0;
            if (channelId >= NumberOfChannelsPerLayer)
            {
                channelId = channelId - NumberOfChannelsPerLayer;
                layerId = 1;
            }

            if (layerId == 0)
                LayerAViewModel?.SetChannelFilterMidiControlSectionId(
                    eventArgs.ChannelType, channelId, eventArgs.SectionId);

            if (layerId == 1)
                LayerBViewModel?.SetChannelFilterMidiControlSectionId(
                    eventArgs.ChannelType, channelId, eventArgs.SectionId);
        }

        if (eventArgs.ChannelType == ChannelType.GroupChannel)
        {
            var channelId = eventArgs.ChannelId;
            var layerId = 0;
            if (channelId >= NumberOfGroupChannelsPerLayer)
            {
                channelId = channelId - NumberOfGroupChannelsPerLayer;
                layerId = 1;
            }

            if (layerId == 0)
                LayerAViewModel?.SetChannelFilterMidiControlSectionId(
                    eventArgs.ChannelType, channelId, eventArgs.SectionId);

            if (layerId == 1)
                LayerBViewModel?.SetChannelFilterMidiControlSectionId(
                    eventArgs.ChannelType, channelId, eventArgs.SectionId);
        }
    }

    private void OnDialSelectionModeChanged(
        DialSelectionModeSelectEventArgs eventArgs)
    {
        if (_logger.IsEnabled(LogLevel.Trace))
            _logger.LogTrace(
                "OnDialSelectionModeChanged, event args={eventArgs}",
                eventArgs);

        if (eventArgs.ChannelType == ChannelType.Channel)
        {
            var channelId = eventArgs.ChannelId;
            var layerId = 0;
            if (channelId >= NumberOfChannelsPerLayer)
            {
                channelId = channelId - NumberOfChannelsPerLayer;
                layerId = 1;
            }

            if (layerId == 0)
                LayerAViewModel?.SetChannelDialMode(
                    eventArgs.ChannelType, channelId, eventArgs.DialMode);

            if (layerId == 1)
                LayerBViewModel?.SetChannelDialMode(
                    eventArgs.ChannelType, channelId, eventArgs.DialMode);
        }

        if (eventArgs.ChannelType == ChannelType.GroupChannel)
        {
            var channelId = eventArgs.ChannelId;
            var layerId = 0;
            if (channelId >= NumberOfGroupChannelsPerLayer)
            {
                channelId = channelId - NumberOfGroupChannelsPerLayer;
                layerId = 1;
            }

            if (layerId == 0)
                LayerAViewModel?.SetChannelDialMode(
                    eventArgs.ChannelType, channelId, eventArgs.DialMode);

            if (layerId == 1)
                LayerBViewModel?.SetChannelDialMode(
                    eventArgs.ChannelType, channelId, eventArgs.DialMode);
        }
    }

    private void OnSelectedChannelChanged(ChannelSelectEventArgs eventArgs)
    {
        if (_logger.IsEnabled(LogLevel.Trace))
            _logger.LogTrace(
                "OnSelectedChannelChanged, event args={eventArgs}",
                eventArgs);

        var (channelId, layerId) = eventArgs.ChannelType switch
        {
            ChannelType.Channel => (eventArgs.ChannelId >=
                                    NumberOfChannelsPerLayer) switch
            {
                true => (eventArgs.ChannelId - NumberOfChannelsPerLayer, 1),
                false => (eventArgs.ChannelId, 0)
            },
            ChannelType.GroupChannel => (eventArgs.ChannelId >=
                                         NumberOfGroupChannelsPerLayer) switch
            {
                true => (eventArgs.ChannelId - NumberOfGroupChannelsPerLayer,
                    1),
                false => (eventArgs.ChannelId, 0)
            },
            _ => throw new ArgumentOutOfRangeException(
                $"Channel type invalid {eventArgs.ChannelType}")
        };

        LayerAViewModel?.ClearSelectedChannel();
        LayerBViewModel?.ClearSelectedChannel();

        if (layerId == 0)
        {
            switch (eventArgs.ChannelType)
            {
                case ChannelType.Channel:
                    LayerAViewModel?.SetSelectedChannel((int)channelId);
                    break;
                case ChannelType.GroupChannel:
                    LayerAViewModel?.SetSelectedGroupChannel((int)channelId);
                    break;
            }
        }
        else
        {
            switch (eventArgs.ChannelType)
            {
                case ChannelType.Channel:
                    LayerBViewModel?.SetSelectedChannel((int)channelId);
                    break;
                case ChannelType.GroupChannel:
                    LayerBViewModel?.SetSelectedGroupChannel((int)channelId);
                    break;
            }
        }
    }

    private void OnLayerSelected(LayerSelectEventArgs eventArgs)
    {
        if (_logger.IsEnabled(LogLevel.Trace))
            _logger.LogTrace(
                "OnLayerSelected, event args={eventArgs}",
                eventArgs);

        switch (eventArgs.LayerId)
        {
            case 0:
                _ = ActivateLayerA();
                break;
            case 1:
                _ = ActivateLayerB();
                break;
        }
    }

    private void OnMoveFilterParamsPageRight(
        FilterParamsSectionMovePagesEventArgs eventArgs)
    {
        if (_logger.IsEnabled(LogLevel.Trace))
            _logger.LogTrace(
                "OnMoveFilterParamsPageRight, event args={eventArgs}",
                eventArgs);

        LayerAViewModel?.ActivateNextPluginPage();
        LayerBViewModel?.ActivateNextPluginPage();
    }

    private void OnMoveFilterParamsPageLeft(
        FilterParamsSectionMovePagesEventArgs eventArgs)
    {
        if (_logger.IsEnabled(LogLevel.Trace))
            _logger.LogTrace(
                "OnMoveFilterParamsPageLeft, event args={eventArgs}",
                eventArgs);

        LayerAViewModel?.ActivatePreviousPluginPage();
        LayerBViewModel?.ActivatePreviousPluginPage();
    }

    public void ShowMidiParameterChangeMonitorWindow()
    {
        _logger.LogTrace("ShowMidiParameterChangeMonitorWindow");

        if (_midiParameterMonitorWindow is not null &&
            _midiParameterMonitorWindow.IsVisible) return;

        var logger = _loggerFactory
            .CreateLogger<MidiParameterChangeMonitorViewModel>();

        var viewModel = new MidiParameterChangeMonitorViewModel(logger,
            _midiControllerService, Fr.Sonic.FrSonic.NodeRegistry);

        _midiParameterMonitorWindow = new MidiParameterChangeMonitorWindow()
        {
            DataContext = viewModel
        };

        _midiParameterMonitorWindow.Show();
    }

    public void ShowGlobalMasterWindow()
    {
        _logger.LogTrace("ShowGlobalMasterWindow");

        if (_globalMasterWindow is not null && _globalMasterWindow.IsVisible) return;

        _ = ShowGlobalMasterWindowAsync();
    }

    private async Task ShowGlobalMasterWindowAsync()
    {
        var viewModel = await EnsureGlobalMasterViewModelAsync();
        if (viewModel is null) return;
        _globalMasterWindow = new GlobalMasterWindow
        {
            DataContext = viewModel
        };
        _globalMasterWindow.Show();
    }

    public void ShowMicChannelWindow()
    {
        _logger.LogTrace("ShowMicChannelWindow");

        if (_micChannelWindow is not null && _micChannelWindow.IsVisible)
        {
            _micChannelWindow.Activate();
            return;
        }

        _ = ShowMicChannelWindowAsync();
    }

    private async Task ShowMicChannelWindowAsync()
    {
        var viewModel = await EnsureMicChannelsViewModelAsync();
        if (viewModel is null) return;
        _micChannelWindow = new MicChannelWindow
        {
            DataContext = viewModel
        };
        _micChannelWindow.Show();
    }

    public void ShowMixerOverviewWindow()
    {
        _logger.LogTrace("ShowMixerOverviewWindow");

        if (_mixerOverviewWindow is not null)
        {
            _mixerOverviewWindow.Activate();
            return;
        }

        _ = ShowMixerOverviewWindowAsync();
    }

    private async Task ShowMixerOverviewWindowAsync()
    {
        var globalMaster = await EnsureGlobalMasterViewModelAsync();
        if (globalMaster is null || LayerAViewModel is null ||
            LayerBViewModel is null)
            return;

        _mixerOverviewViewModel = new MixerOverviewViewModel(LayerAViewModel,
            LayerBViewModel, globalMaster);

        _mixerOverviewWindow = new MixerOverviewWindow
        {
            DataContext = _mixerOverviewViewModel
        };
        _mixerOverviewWindow.Closed += (_, _) =>
        {
            // Only disposes the overview's own row-list wrappers. LayerAViewModel,
            // LayerBViewModel and globalMaster are shared, app-lifetime instances owned
            // by this class — never dispose them here.
            _mixerOverviewViewModel?.Dispose();
            _mixerOverviewViewModel = null;
            _mixerOverviewWindow = null;
        };
        _mixerOverviewWindow.Show();
    }

    public async Task SaveMixer()
    {
        if (_currentMixerId == Guid.Empty)
        {
            await SaveMixerAs();
            return;
        }

        await StoreMixerAsync(_currentMixerId, _currentMixerName);
    }

    public async Task SaveMixerAs()
    {
        var viewModel = new MixerNameDialogViewModel(_currentMixerName);
        var dialog = new MixerNameDialog { DataContext = viewModel };
        await dialog.ShowDialog(WindowTools.GetMainWindow()!);
        if (!viewModel.DialogResult) return;

        var id = Guid.NewGuid();
        var name = viewModel.Name.Trim();
        await StoreMixerAsync(id, name);
        _currentMixerId = id;
        _currentMixerName = name;
    }

    public async Task LoadMixer()
    {
        if (Transport.TransportState != Fr.Sonic.Sync.SyncTransportState.Stopped)
            return;

        var appDataService = Locator.Current.GetService<IAppDataService>();
        var configurationService =
            Locator.Current.GetService<MixerConfigurationService>();
        if (appDataService is null || configurationService is null) return;

        var mixers = await appDataService.GetAllMixers();
        var viewModel = new LoadMixerDialogViewModel(mixers);
        var dialog = new LoadMixerDialog { DataContext = viewModel };
        await dialog.ShowDialog(WindowTools.GetMainWindow()!);
        if (!viewModel.DialogResult || viewModel.SelectedMixer is null) return;

        var globalMaster = await EnsureGlobalMasterViewModelAsync();
        if (globalMaster is null || LayerAViewModel is null ||
            LayerBViewModel is null)
            return;

        var mic = await EnsureMicChannelsViewModelAsync();

        await configurationService.ApplyAsync(viewModel.SelectedMixer,
            LayerAViewModel, LayerBViewModel, globalMaster, mic?.Mic1,
            mic?.Mic2);
        _currentMixerId = viewModel.SelectedMixer.Id;
        _currentMixerName = viewModel.SelectedMixer.Name;
    }

    private async Task StoreMixerAsync(Guid id, string name)
    {
        var globalMaster = await EnsureGlobalMasterViewModelAsync();
        if (globalMaster is null || LayerAViewModel is null ||
            LayerBViewModel is null)
            return;

        var mic = await EnsureMicChannelsViewModelAsync();

        var appDataService = Locator.Current.GetService<IAppDataService>();
        var configurationService =
            Locator.Current.GetService<MixerConfigurationService>();
        if (appDataService is null || configurationService is null) return;

        var configuration = configurationService.Capture(id, name,
            LayerAViewModel, LayerBViewModel, globalMaster, mic?.Mic1,
            mic?.Mic2);
        await appDataService.CreateMixer(configuration);
    }

    public async Task LoadOrCreateMixerByNameAsync(string mixerName)
    {
        var globalMaster = await EnsureGlobalMasterViewModelAsync();
        if (globalMaster is null || LayerAViewModel is null ||
            LayerBViewModel is null)
            return;

        var mic = await EnsureMicChannelsViewModelAsync();

        var appDataService = Locator.Current.GetService<IAppDataService>();
        var configurationService =
            Locator.Current.GetService<MixerConfigurationService>();
        if (appDataService is null || configurationService is null) return;

        var mixers = await appDataService.GetAllMixers();
        var existing = mixers.FirstOrDefault(m => m.Name == mixerName);

        if (existing is not null)
        {
            await configurationService.ApplyAsync(existing, LayerAViewModel,
                LayerBViewModel, globalMaster, mic?.Mic1, mic?.Mic2);
            _currentMixerId = existing.Id;
            _currentMixerName = existing.Name;
        }
        else
        {
            var id = Guid.NewGuid();
            await StoreMixerAsync(id, mixerName);
            _currentMixerId = id;
            _currentMixerName = mixerName;
        }
    }

    private async Task<GlobalMasterViewModel?>
        EnsureGlobalMasterViewModelAsync()
    {
        if (_globalMasterViewModel is not null)
            return _globalMasterViewModel;

        await EnsureLayerViewModelsAsync();
        var mixerService = Locator.Current.GetService<IMixerService>();
        var globalMaster = mixerService?.CurrentMixer?.GlobalMaster;
        var layerA = LayerAViewModel?.MasterChannels?
            .OfType<MasterChannelViewModel>().FirstOrDefault();
        var layerB = LayerBViewModel?.MasterChannels?
            .OfType<MasterChannelViewModel>().FirstOrDefault();
        if (globalMaster is null || layerA is null || layerB is null)
            return null;

        _globalMasterViewModel = new(globalMaster, layerA, layerB,
            mixerService?.CurrentMixer?.Cue,
            LayerAViewModel!.AudioToRoutingTargetsMasterChannel);
        return _globalMasterViewModel;
    }

    private async Task<MicChannelsViewModel?> EnsureMicChannelsViewModelAsync()
    {
        if (_micChannelsViewModel is not null)
            return _micChannelsViewModel;

        await EnsureLayerViewModelsAsync();
        var mixerService = Locator.Current.GetService<IMixerService>();
        var viewModelService =
            Locator.Current.GetService<IMixerViewModelService>();
        if (mixerService?.CurrentMixer?.Mic is not { } mic ||
            mixerService.CurrentMixer?.Mic2 is not { } mic2 ||
            viewModelService is null)
            return null;

        var mic1ViewModel = viewModelService.ConvertMicChannel(mic, 1,
            ReactiveCommand.Create(() => { }));
        var mic2ViewModel = viewModelService.ConvertMicChannel(mic2, 2,
            ReactiveCommand.Create(() => { }));
        _micChannelsViewModel = new(mic1ViewModel, mic2ViewModel);
        return _micChannelsViewModel;
    }

    private async Task EnsureLayerViewModelsAsync()
    {
        await _layerInitializationLock.WaitAsync();
        try
        {
            var mixerService = Locator.Current.GetService<IMixerService>();
            var viewModelService =
                Locator.Current.GetService<IMixerViewModelService>();
            if (mixerService is null || viewModelService is null) return;
            var createdDefaultMixer = mixerService.CurrentMixer is null;
            if (mixerService.CurrentMixer is null)
                await CreateMixer();

            LayerAViewModel ??=
                await viewModelService.ConvertCurrentMixerToViewModel(0);
            LayerBViewModel ??=
                await viewModelService.ConvertCurrentMixerToViewModel(1);

            IsMicChannelEnabled = mixerService.CurrentMixer?.Mic is not null;
            if (IsMicChannelEnabled &&
                mixerService.CurrentMixer?.Mic is { } mic &&
                mixerService.CurrentMixer?.Mic2 is { } mic2)
                _micChannelsViewModel ??= new(
                    viewModelService.ConvertMicChannel(mic, 1,
                        ReactiveCommand.Create(() => { })),
                    viewModelService.ConvertMicChannel(mic2, 2,
                        ReactiveCommand.Create(() => { })));

            if (LayerAViewModel is not null && LayerBViewModel is not null)
            {
                if (createdDefaultMixer &&
                    mixerService.CurrentMixer?.GlobalMaster is { } globalMaster &&
                    LayerAViewModel.MasterChannels?
                        .OfType<MasterChannelViewModel>().FirstOrDefault()
                        is { } layerA &&
                    LayerBViewModel.MasterChannels?
                        .OfType<MasterChannelViewModel>().FirstOrDefault()
                        is { } layerB)
                {
                    _globalMasterViewModel = new(globalMaster, layerA, layerB,
                        mixerService.CurrentMixer.Cue,
                        LayerAViewModel.AudioToRoutingTargetsMasterChannel);

                    var configurationService =
                        Locator.Current.GetService<MixerConfigurationService>();
                    if (configurationService is not null)
                    {
                        var defaultConfiguration =
                            configurationService.CreateDefault(LayerAViewModel,
                                LayerBViewModel, _globalMasterViewModel,
                                _micChannelsViewModel?.Mic1,
                                _micChannelsViewModel?.Mic2);
                        await configurationService.ApplyAsync(
                            defaultConfiguration, LayerAViewModel,
                            LayerBViewModel, _globalMasterViewModel,
                            _micChannelsViewModel?.Mic1,
                            _micChannelsViewModel?.Mic2);
                    }
                }

                IsInitialized = true;
                viewModelService.SetupControllers();
            }
        }
        finally
        {
            _layerInitializationLock.Release();
        }
    }

    public void ShowMonitoringWindow()
    {
        _logger.LogTrace("ShowMonitoringWindow");

        if (_monitoringWindow is not null && _monitoringWindow.IsVisible)
            return;

        var mixerService = Locator.Current.GetService<IMixerService>();
        var loopback = mixerService?.CurrentMixer?.MonitoringLoopback;

        _monitoringWindow = new MonitoringWindow()
        {
            DataContext = new MonitoringViewModel(loopback)
        };

        _monitoringWindow.Show();
    }

    public void ShowSynchronizationWindow()
    {
        _logger.LogTrace("ShowSynchronizationWindow");

        if (_synchronizationWindow is not null &&
            _synchronizationWindow.IsVisible) return;

        _synchronizationWindow = new SynchronizationWindow()
        {
            DataContext = new SynchronizationViewModel(
                Fr.Sonic.FrSonic.NodeRegistry.Objects
                    .FirstOrDefault(node => node.Name == "se.sync_master"))
        };

        _synchronizationWindow.Show();
    }

    public void ShowMidiSyncWindow()
    {
        _logger.LogTrace("ShowMidiSyncWindow");

        if (_midiSyncWindow is not null &&
            _midiSyncWindow.IsVisible) return;

        _midiSyncWindow = new MidiSyncWindow()
        {
            DataContext = new MidiSyncViewModel()
        };

        _midiSyncWindow.Show();
    }

    public void ShowClickSyncWindow()
    {
        _logger.LogTrace("ShowClickSyncWindow");

        if (_clickSyncWindow is not null &&
            _clickSyncWindow.IsVisible) return;

        _clickSyncWindow = new ClickSyncWindow()
        {
            DataContext = new ClickSyncViewModel()
        };

        _clickSyncWindow.Show();
    }

    public void ShowMidiRouterWindow()
    {
        _logger.LogTrace("ShowMidiRouterWindow");

        if (_midiRouterWindow is not null &&
            _midiRouterWindow.IsVisible) return;

        _midiRouterWindow = new MidiRouterWindow()
        {
            DataContext = new MidiRouterViewModel()
        };

        _midiRouterWindow.Show();
    }

    public void ShowVirtualInputsWindow()
    {
        _logger.LogTrace("ShowVirtualInputsWindow");

        if (_virtualInputsWindow is not null &&
            _virtualInputsWindow.IsVisible) return;

        var virtualInputsService =
            Locator.Current.GetService<IVirtualInputService>();

        var wireplumberService =
            Locator.Current.GetService<IWireplumberService>();

        var viewModel =
            new VirtualInputsViewModel(
                wireplumberService!,
                virtualInputsService!);

        _virtualInputsWindow = new VirtualInputsWindow()
        {
            DataContext = viewModel
        };

        _virtualInputsWindow.Show();
    }

    public void ShowVirtualOutputsWindow()
    {
        _logger.LogTrace("ShowVirtualOutputsWindow");

        if (_virtualOutputsWindow is not null &&
            _virtualOutputsWindow.IsVisible) return;

        var virtualOutputService =
            Locator.Current.GetService<IVirtualOutputService>();
        var wireplumberService =
            Locator.Current.GetService<IWireplumberService>();

        _virtualOutputsWindow = new VirtualOutputsWindow
        {
            DataContext = new VirtualOutputsViewModel(
                wireplumberService!,
                virtualOutputService!)
        };

        _virtualOutputsWindow.Show();
    }

    public void ShowJackInputPortsWindow()
    {
        _logger.LogTrace("ShowJackInputPortsWindow");

        if (_jackInputPortsWindow is not null &&
            _jackInputPortsWindow.IsVisible) return;

        var jackInputPortService =
            Locator.Current.GetService<IJackInputPortService>();
        var wireplumberService =
            Locator.Current.GetService<IWireplumberService>();

        _jackInputPortsWindow = new JackInputPortsWindow
        {
            DataContext = new JackInputPortsViewModel(
                jackInputPortService!,
                wireplumberService!)
        };

        _jackInputPortsWindow.Show();
    }

    public void ShowRecordingPickUpsWindow()
    {
        _logger.LogTrace("ShowRecordingPickUpsWindow");

        if (_recordingPickUpsWindow is not null &&
            _recordingPickUpsWindow.IsVisible) return;

        var service = Locator.Current.GetService<IRecordingPickUpService>();
        var wireplumberService = Locator.Current.GetService<IWireplumberService>();

        _recordingPickUpsWindow = new RecordingPickUpsWindow
        {
            DataContext = new RecordingPickUpsViewModel(service!, wireplumberService!)
        };

        _recordingPickUpsWindow.Show();
    }

    public void ShowFilterGraphManagerWindow()
    {
        _logger.LogTrace("ShowFilterGraphManagerWindow");

        if (_filterGraphWindow is not null &&
            _filterGraphWindow.IsVisible) return;

        var appDataService = Locator.Current.GetService<IAppDataService>();

        var viewModel = new FilterGraphManagerViewModel(appDataService!);

        _filterGraphWindow = new FilterGraphWindow()
        {
            DataContext = viewModel
        };

        _filterGraphWindow.Show();
    }

    public void ShowModuleManagerWindow()
    {
        _logger.LogTrace("ShowModuleManagerWindow");

        if (_moduleManagerWindow is not null &&
            _moduleManagerWindow.IsVisible) return;

        var appDataService = Locator.Current.GetService<IAppDataService>();

        var viewModel = new ModuleManagerViewModel(appDataService!);

        _moduleManagerWindow = new ModuleManagerWindow()
        {
            DataContext = viewModel
        };

        _moduleManagerWindow.Show();
    }

    public void ShowObjectBrowserWindow()
    {
        _logger.LogTrace("ShowObjectBrowserWindow");

        if (_objectBrowserWindow is not null &&
            _objectBrowserWindow.IsVisible) return;

        var appDataService = Locator.Current.GetService<IAppDataService>();

        var viewModel = new ObjectBrowserViewModel(appDataService!);

        _objectBrowserWindow = new ObjectBrowserWindow()
        {
            DataContext = viewModel
        };

        _objectBrowserWindow.Show();
    }

    public void ShowMetadataBrowserWindow()
    {
        _logger.LogTrace("ShowMetadataBrowserWindow");

        if (_metadataManagerWindow is not null &&
            _metadataManagerWindow.IsVisible) return;

        var appDataService = Locator.Current.GetService<IAppDataService>();

        var viewModel = new MetadataViewModel(appDataService!);

        _metadataManagerWindow = new MetadataWindow()
        {
            DataContext = viewModel
        };

        _metadataManagerWindow.Show();
    }

    public void ExitCommand()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }

    public void ShowPreferencesWindow()
    {
        _logger.LogTrace("ShowPreferencesWindow");

        if (_preferencesWindow is not null &&
            _preferencesWindow.IsVisible) return;

        var wireplumberService =
            Locator.Current.GetService<IWireplumberService>();

        var preferenceService =
            Locator.Current.GetService<IPreferenceService>();

        var viewModel =
            new PreferencesViewModel(wireplumberService!, preferenceService!);

        _preferencesWindow = new PreferencesWindow()
        {
            DataContext = viewModel
        };

        _preferencesWindow.Show();
    }

    public Task NavigateToMixerV2ViewLayerA()
    {
        _logger.LogTrace("NavigateToMixerV2ViewLayerA");

        _midiControllerService.SetSelectedLayer(0);
        return ActivateLayerA();
    }

    private async Task ActivateLayerA()
    {
        _logger.LogTrace("ActivateLayerA");

        MixerViewV2LayerAViewSelected = true;
        MixerViewV2LayerBViewSelected = false;

        await EnsureLayerViewModelsAsync();
    }

    private static Task CreateMixer()
    {
        var mixerService = Locator.Current.GetService<IMixerService>();
        return mixerService!.NewCurrentMixer("Default Mixer");
    }

    public Task NavigateToMixerV2ViewLayerB()
    {
        _logger.LogTrace("NavigateToMixerV2ViewLayerB");

        _midiControllerService.SetSelectedLayer(1);
        return ActivateLayerB();
    }

    private async Task ActivateLayerB()
    {
        _logger.LogTrace("ActivateLayerB");

        MixerViewV2LayerAViewSelected = false;
        MixerViewV2LayerBViewSelected = true;

        await EnsureLayerViewModelsAsync();
    }

    public void Dispose()
    {
        Transport.Dispose();
        _drumMixerWindow?.Close();
        _externalEffectsWindow?.Close();
        _drumMixerViewModel?.Dispose();
        if (_drumMixerService is not null)
            _drumMixerService.Changed -= OnDrumMixerChanged;
        _globalMasterViewModel?.Dispose();
        _micChannelWindow?.Close();
        _micChannelsViewModel?.Mic1.Dispose();
        _micChannelsViewModel?.Mic2.Dispose();
        Locator.Current.GetService<MixerConfigurationService>()?.Dispose();
        _layerInitializationLock.Dispose();
        _midiControllerService.LayerChanged -= OnLayerSelected;
        _midiControllerService.FilterParamsSectionMovedRight -=
            OnMoveFilterParamsPageRight;

        _midiControllerService.FilterParamsSectionMovedLeft -=
            OnMoveFilterParamsPageLeft;

        _midiControllerService.SelectedChannelChanged -=
            OnSelectedChannelChanged;

        _midiControllerService.DialSelectionModeChanged -=
            OnDialSelectionModeChanged;

        _midiControllerService.SelectedFilterParamsSectionChanged -=
            OnFilterMidiControlSectionIdChanged;

        _traktorZ1Service.FilterSectionChanged -= OnTraktorZ1FilterSectionChanged;

        GC.SuppressFinalize(this);
    }
}
