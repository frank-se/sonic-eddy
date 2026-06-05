using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Fr.Sonic;
using Fr.Sonic.PInvoke;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using SonicEddy.Services.AppData;
using SonicEddy.Services.Midi;
using SonicEddy.Services.MixerServiceV2;
using SonicEddy.Services.TraktorZ1;
using SonicEddy.Services.MixerViewModels;
using SonicEddy.Services.Preferences;
using SonicEddy.Services.VirtualInputs;
using SonicEddy.Services.VirtualOutputs;
using SonicEddy.Services.Wireplumber;
using SonicEddy.ViewModels.FilterGraphManagerViewModels;
using SonicEddy.ViewModels.MetadataViewModels;
using SonicEddy.ViewModels.MidiParameterChangeMonitorViewModels;
using SonicEddy.ViewModels.MidiRouterViewModels;
using SonicEddy.ViewModels.MixerViewModelsV2;
using SonicEddy.ViewModels.ModuleManagerViewModels;
using SonicEddy.ViewModels.ObjectBrowserViewModels;
using SonicEddy.ViewModels.PreferencesViewModels;
using SonicEddy.ViewModels.MidiSyncViewModels;
using SonicEddy.ViewModels.SynchronizationViewModels;
using SonicEddy.ViewModels.MonitoringViewModels;
using SonicEddy.ViewModels.TransportViewModels;
using SonicEddy.ViewModels.GlobalMasterViewModels;
using SonicEddy.ViewModels.VirtualInputsViewModels;
using SonicEddy.ViewModels.VirtualOutputsViewModels;
using SonicEddy.Views.FilterGraphManagerViews;
using SonicEddy.Views.GlobalMasterViews;
using SonicEddy.Views.MetadataViews;
using SonicEddy.Views.MidiParameterChangeMonitorView;
using SonicEddy.Views.MidiRouterViews;
using SonicEddy.Views.ModuleManagerViews;
using SonicEddy.Views.ObjectBrowserViews;
using SonicEddy.Views.PreferencesViews;
using SonicEddy.Views.MidiSyncViews;
using SonicEddy.Views.SynchronizationViews;
using SonicEddy.Views.MonitoringViews;
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
    private Window? _monitoringWindow;
    private Window? _midiParameterMonitorWindow;
    private Window? _objectBrowserWindow;
    private Window? _metadataManagerWindow;
    private Window? _moduleManagerWindow;
    private Window? _filterGraphWindow;
    private Window? _virtualInputsWindow;
    private Window? _virtualOutputsWindow;
    private Window? _preferencesWindow;
    private Window? _synchronizationWindow;
    private Window? _midiSyncWindow;
    private Window? _midiRouterWindow;

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

        var mixerService = Locator.Current.GetService<IMixerService>();
        var globalMaster = mixerService?.CurrentMixer?.GlobalMaster;
        if (globalMaster is null) return;

        var layerA = LayerAViewModel?.MasterChannels?.Count > 0
            ? LayerAViewModel.MasterChannels[0] as MasterChannelViewModel
            : null;
        var layerB = LayerBViewModel?.MasterChannels?.Count > 0
            ? LayerBViewModel.MasterChannels[0] as MasterChannelViewModel
            : null;
        if (layerA is null || layerB is null) return;

        _globalMasterWindow = new GlobalMasterWindow
        {
            DataContext = new GlobalMasterViewModel(globalMaster, layerA, layerB,
                mixerService?.CurrentMixer?.Cue)
        };
        _globalMasterWindow.Show();
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

        if (LayerAViewModel is not null)
        {
            _logger.LogTrace("Layer A model already exists, done!");
            return;
        }

        var mixerService = Locator.Current.GetService<IMixerService>();

        var mixerViewModelService =
            Locator.Current.GetService<IMixerViewModelService>();

        if (mixerService is null || mixerViewModelService is null) return;

        if (mixerService.CurrentMixer is null)
            await CreateMixer();

        Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
        {
            var mixerView =
                await mixerViewModelService
                    .ConvertCurrentMixerToViewModel(0);

            if (mixerView is not null)
            {
                LayerAViewModel = mixerView;
                IsInitialized = true;
                mixerViewModelService.SetupControllers();
            }
        });
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

        if (LayerBViewModel is not null)
        {
            _logger.LogTrace("Layer B model already exists, done!");
            return;
        }

        var mixerService = Locator.Current.GetService<IMixerService>();

        var mixerViewModelService =
            Locator.Current.GetService<IMixerViewModelService>();

        if (mixerService is null || mixerViewModelService is null) return;

        if (mixerService.CurrentMixer is null)
            await CreateMixer();

        Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
        {
            var mixerView =
                await mixerViewModelService
                    .ConvertCurrentMixerToViewModel(1);

            if (mixerView is not null)
                LayerBViewModel = mixerView;
        });
    }

    public void Dispose()
    {
        Transport.Dispose();
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
