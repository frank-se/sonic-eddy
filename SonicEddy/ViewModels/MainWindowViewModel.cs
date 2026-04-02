using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Fr.Pw.Midi;
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
using SonicEddy.ViewModels.MixerViewModelsV2;
using SonicEddy.ViewModels.ModuleManagerViewModels;
using SonicEddy.ViewModels.ObjectBrowserViewModels;
using SonicEddy.ViewModels.PreferencesViewModels;
using SonicEddy.ViewModels.VirtualInputsViewModels;
using SonicEddy.Views.FilterGraphManagerViews;
using SonicEddy.Views.MetadataViews;
using SonicEddy.Views.ModuleManagerViews;
using SonicEddy.Views.ObjectBrowserViews;
using SonicEddy.Views.PreferencesViews;
using SonicEddy.Views.VirtualInputsViews;
using Splat;

namespace SonicEddy.ViewModels;

public class MainWindowViewModel : ViewModelBase, IDisposable
{
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

    public MainWindowViewModel(IMidiControllerService midiControllerService)
    {
        _midiControllerService = midiControllerService;

        _midiControllerService.LayerChanged += OnLayerSelected;

        _ = NavigateToMixerV2ViewLayerA();
    }

    private void OnLayerSelected(LayerSelectEventArgs eventArgs)
    {
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
        GC.SuppressFinalize(this);
    }
}