using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Fr.Pw.Midi;
using Fr.Pw.Midi.PInvoke;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using SonicEddy.Services.AppData;
using SonicEddy.Services.Midi;
using SonicEddy.Services.MixerServiceV2;
using SonicEddy.Services.MixerViewModels;
using SonicEddy.Services.Preferences;
using SonicEddy.Services.VirtualInputs;
using SonicEddy.Services.Wireplumber;
using SonicEddy.ViewModels.FilterGraphManagerViewModels;
using SonicEddy.ViewModels.MetadataViewModels;
using SonicEddy.ViewModels.MidiParameterChangeMonitorViewModels;
using SonicEddy.ViewModels.MixerViewModelsV2;
using SonicEddy.ViewModels.ModuleManagerViewModels;
using SonicEddy.ViewModels.ObjectBrowserViewModels;
using SonicEddy.ViewModels.PreferencesViewModels;
using SonicEddy.ViewModels.VirtualInputsViewModels;
using SonicEddy.Views.FilterGraphManagerViews;
using SonicEddy.Views.MetadataViews;
using SonicEddy.Views.MidiParameterChangeMonitorView;
using SonicEddy.Views.ModuleManagerViews;
using SonicEddy.Views.ObjectBrowserViews;
using SonicEddy.Views.PreferencesViews;
using SonicEddy.Views.VirtualInputsViews;
using Splat;

namespace SonicEddy.ViewModels;

public class MainWindowViewModel : ViewModelBase, IDisposable
{
    public MainWindowViewModel(IMidiControllerService midiControllerService,
        ILogger<MainWindowViewModel> logger, ILoggerFactory loggerFactory)
    {
        _midiControllerService = midiControllerService;
        _logger = logger;
        _loggerFactory = loggerFactory;

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

        _ = NavigateToMixerV2ViewLayerA();
    }

    private const int NumberOfGroupChannels = 8;
    private const int NumberOfGroupChannelsPerLayer = NumberOfGroupChannels / 2;
    private const int NumberOfChannels = 16;
    private const int NumberOfChannelsPerLayer = NumberOfChannels / 2;

    private Window? _midiParameterMonitorWindow;
    private Window? _objectBrowserWindow;
    private Window? _metadataManagerWindow;
    private Window? _moduleManagerWindow;
    private Window? _filterGraphWindow;
    private Window? _virtualInputsWindow;
    private Window? _preferencesWindow;

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
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly ILoggerFactory _loggerFactory;

    private void OnFilterMidiControlSectionIdChanged(
        FilterParamsSectionSelectEventArgs eventArgs)
    {
        if (eventArgs.ChannelType == ChannelType.Channel)
        {
            var channelId = eventArgs.ChannelId;
            var layerId = 0;
            if (channelId > NumberOfChannelsPerLayer)
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
            if (channelId > NumberOfGroupChannelsPerLayer)
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
        if (eventArgs.ChannelType == ChannelType.Channel)
        {
            var channelId = eventArgs.ChannelId;
            var layerId = 0;
            if (channelId > NumberOfChannelsPerLayer)
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
            if (channelId > NumberOfGroupChannelsPerLayer)
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
        _logger.LogTrace("OnSelectedChannelChanged");

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
        _logger.LogTrace("OnLayerSelected");

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
        _logger.LogTrace("OnMoveFilterParamsPageRight");

        LayerAViewModel?.ActivateNextPluginPage();
        LayerBViewModel?.ActivateNextPluginPage();
    }

    private void OnMoveFilterParamsPageLeft(
        FilterParamsSectionMovePagesEventArgs eventArgs)
    {
        _logger.LogTrace("OnMoveFilterParamsPageLeft");

        LayerAViewModel?.ActivatePreviousPluginPage();
        LayerBViewModel?.ActivatePreviousPluginPage();
    }

    public void ShowMidiParameterChangeMonitorWindow()
    {
        if (_midiParameterMonitorWindow is not null &&
            _midiParameterMonitorWindow.IsVisible) return;

        var logger = _loggerFactory
            .CreateLogger<MidiParameterChangeMonitorViewModel>();

        var viewModel = new MidiParameterChangeMonitorViewModel(logger,
            _midiControllerService, Fr.Wireplumber.Wireplumber.NodeRegistry);

        _midiParameterMonitorWindow = new MidiParameterChangeMonitorWindow()
        {
            DataContext = viewModel
        };

        _midiParameterMonitorWindow.Show();
    }

    public void ShowVirtualInputsWindow()
    {
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

    public void ShowFilterGraphManagerWindow()
    {
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

    public void ShowPreferencesWindow()
    {
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
        _midiControllerService.SetSelectedLayer(0);
        return ActivateLayerA();
    }

    private async Task ActivateLayerA()
    {
        MixerViewV2LayerAViewSelected = true;
        MixerViewV2LayerBViewSelected = false;

        if (LayerAViewModel == null)
        {
            var mixerService = Locator.Current.GetService<IMixerService>();

            var mixerViewModelService =
                Locator.Current.GetService<IMixerViewModelService>();

            if (mixerService is null || mixerViewModelService is null) return;

            if (mixerService.CurrentMixer is null)
                await mixerService.NewCurrentMixer("Default Mixer");

            Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
            {
                var mixerView =
                    await mixerViewModelService
                        .ConvertCurrentMixerToViewModel(0);

                if (mixerView is not null)
                    LayerAViewModel = mixerView;
            });
        }
    }

    public Task NavigateToMixerV2ViewLayerB()
    {
        _midiControllerService.SetSelectedLayer(1);
        return ActivateLayerB();
    }

    private async Task ActivateLayerB()
    {
        MixerViewV2LayerAViewSelected = false;
        MixerViewV2LayerBViewSelected = true;

        if (LayerBViewModel == null)
        {
            var mixerService = Locator.Current.GetService<IMixerService>();

            var mixerViewModelService =
                Locator.Current.GetService<IMixerViewModelService>();

            if (mixerService is null || mixerViewModelService is null) return;

            if (mixerService.CurrentMixer is null)
                await mixerService.NewCurrentMixer("Default Mixer");

            Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
            {
                var mixerView =
                    await mixerViewModelService
                        .ConvertCurrentMixerToViewModel(1);

                if (mixerView is not null)
                    LayerBViewModel = mixerView;
            });
        }
    }

    public void Dispose()
    {
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

        GC.SuppressFinalize(this);
    }
}