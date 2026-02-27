using System.Threading.Tasks;
using Avalonia.Controls;
using ReactiveUI;
using SonicEddy.Services.AppData;
using SonicEddy.Services.MixerServiceV2;
using SonicEddy.Services.MixerViewModels;
using SonicEddy.Services.Preferences;
using SonicEddy.Services.VirtualInputs;
using SonicEddy.Services.Wireplumber;
using SonicEddy.ViewModels.CustomControlTesterViewModels;
using SonicEddy.ViewModels.FilterGraphManagerViewModels;
using SonicEddy.ViewModels.GraphControlTesterViewModels;
using SonicEddy.ViewModels.MetadataViewModels;
using SonicEddy.ViewModels.MidiConnectionEditorViewModels;
using SonicEddy.ViewModels.MixerManagerViewModels;
using SonicEddy.ViewModels.ModuleManagerViewModels;
using SonicEddy.ViewModels.ObjectBrowserViewModels;
using SonicEddy.ViewModels.PreferencesViewModels;
using SonicEddy.ViewModels.ProAudioStreamsViewModels;
using SonicEddy.ViewModels.VirtualInputsViewModels;
using SonicEddy.Views.FilterGraphManagerViews;
using SonicEddy.Views.MetadataViews;
using SonicEddy.Views.ModuleManagerViews;
using SonicEddy.Views.ObjectBrowserViews;
using SonicEddy.Views.PreferencesViews;
using SonicEddy.Views.VirtualInputsViews;
using Splat;
using IMixerService = SonicEddy.Services.MixerData.IMixerService;

namespace SonicEddy.ViewModels;

public class MainWindowViewModel : ViewModelBase, IScreen
{
    private Window? _objectBrowserWindow;
    private Window? _metadataManagerWindow;
    private Window? _moduleManagerWindow;
    private Window? _filterGraphWindow;
    private Window? _virtualInputsWindow;
    private Window? _preferencesWindow;

    public RoutingState Router { get; } = new();

    public MainWindowViewModel()
    {
        _ = NavigateToMixerV2View();
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

        var viewModel = new FilterGraphManagerViewModel(appDataService!,
            "filter-graph-builder", this);

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

        var viewModel = new ModuleManagerViewModel(appDataService!,
            "module-manager", this);

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

        var viewModel = new ObjectBrowserViewModel(appDataService!,
            "object-browser",
            this);

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

        var viewModel = new MetadataViewModel(appDataService!,
            "metadata", this);

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

    public bool ProAudioStreamsMenuItemSelected
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool MixerMenuItemSelected
    {
        get;
        set => field =
            this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool MetadataMenuItemSelected
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool ModuleManagerViewSelected
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool FilterGraphBuilderViewSelected
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool FilterGraphManagerViewSelected
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool CustomControlTesterViewSelected
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool MixerManagerViewSelected
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool GraphControlTesterViewSelected
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool MidiConnectionEditorViewSelected
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool MixerViewV2ViewSelected
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public void NavigateToProAudioStreamsAction()
    {
        MixerMenuItemSelected = false;
        ProAudioStreamsMenuItemSelected = true;
        MetadataMenuItemSelected = false;
        ModuleManagerViewSelected = false;
        FilterGraphBuilderViewSelected = false;
        FilterGraphManagerViewSelected = false;
        CustomControlTesterViewSelected = false;
        MixerManagerViewSelected = false;
        GraphControlTesterViewSelected = false;
        MidiConnectionEditorViewSelected = false;
        MixerViewV2ViewSelected = false;
        var appDataService = Locator.Current.GetService<IAppDataService>();
        Router.Navigate.Execute(new ProAudioStreamsViewModel(appDataService!,
            "pro-audio-streams", this));
    }

    public void NavigateToModuleManagerView()
    {
        MixerMenuItemSelected = false;
        ProAudioStreamsMenuItemSelected = false;
        MetadataMenuItemSelected = false;
        ModuleManagerViewSelected = true;
        FilterGraphBuilderViewSelected = false;
        FilterGraphManagerViewSelected = false;
        CustomControlTesterViewSelected = false;
        MixerManagerViewSelected = false;
        GraphControlTesterViewSelected = false;
        MidiConnectionEditorViewSelected = false;
        MixerViewV2ViewSelected = false;
        var appDataService = Locator.Current.GetService<IAppDataService>();
        Router.Navigate.Execute(new ModuleManagerViewModel(appDataService!,
            "module-manager", this));
    }

    public void NavigateToFilterGraphManagerView()
    {
        MixerMenuItemSelected = false;
        ProAudioStreamsMenuItemSelected = false;
        MetadataMenuItemSelected = false;
        ModuleManagerViewSelected = false;
        FilterGraphBuilderViewSelected = false;
        FilterGraphManagerViewSelected = true;
        CustomControlTesterViewSelected = false;
        MixerManagerViewSelected = false;
        GraphControlTesterViewSelected = false;
        MidiConnectionEditorViewSelected = false;
        MixerViewV2ViewSelected = false;
        var appDataService = Locator.Current.GetService<IAppDataService>();
        Router.Navigate.Execute(new FilterGraphManagerViewModel(appDataService!,
            "filter-graph-builder", this));
    }

    public void NavigateToCustomControlTesterView()
    {
        MixerMenuItemSelected = false;
        ProAudioStreamsMenuItemSelected = false;
        MetadataMenuItemSelected = false;
        ModuleManagerViewSelected = false;
        FilterGraphBuilderViewSelected = false;
        FilterGraphManagerViewSelected = false;
        CustomControlTesterViewSelected = true;
        MixerManagerViewSelected = false;
        GraphControlTesterViewSelected = false;
        MidiConnectionEditorViewSelected = false;
        MixerViewV2ViewSelected = false;
        Router.Navigate.Execute(
            new CustomControlTesterViewModel("control-tester", this));
    }

    public void NavigateToMixerManagerAction()
    {
        MixerMenuItemSelected = false;
        ProAudioStreamsMenuItemSelected = false;
        MetadataMenuItemSelected = false;
        ModuleManagerViewSelected = false;
        FilterGraphBuilderViewSelected = false;
        FilterGraphManagerViewSelected = false;
        CustomControlTesterViewSelected = false;
        MixerManagerViewSelected = true;
        GraphControlTesterViewSelected = false;
        MidiConnectionEditorViewSelected = false;
        MixerViewV2ViewSelected = false;
        var mixerService = Locator.Current.GetService<IMixerService>();
        Router.Navigate.Execute(new MixerManagerViewModel("mixer-manager",
            this, mixerService!));
    }

    public void NavigateToGraphControlTesterView()
    {
        MixerMenuItemSelected = false;
        ProAudioStreamsMenuItemSelected = false;
        MetadataMenuItemSelected = false;
        ModuleManagerViewSelected = false;
        FilterGraphBuilderViewSelected = false;
        FilterGraphManagerViewSelected = false;
        CustomControlTesterViewSelected = false;
        MixerManagerViewSelected = false;
        GraphControlTesterViewSelected = true;
        MidiConnectionEditorViewSelected = false;
        MixerViewV2ViewSelected = false;
        Router.Navigate.Execute(
            new GraphControlTesterViewModel("graph-tester", this));
    }

    public void NavigateToMidiConnectionEditorView()
    {
        MixerMenuItemSelected = false;
        ProAudioStreamsMenuItemSelected = false;
        MetadataMenuItemSelected = false;
        ModuleManagerViewSelected = false;
        FilterGraphBuilderViewSelected = false;
        FilterGraphManagerViewSelected = false;
        CustomControlTesterViewSelected = false;
        MixerManagerViewSelected = false;
        GraphControlTesterViewSelected = false;
        MidiConnectionEditorViewSelected = true;
        MixerViewV2ViewSelected = false;
        var wireplumberService =
            Locator.Current.GetService<IWireplumberService>();
        var ports = wireplumberService.GetMidiPorts();
        Router.Navigate.Execute(
            new MidiConnectionEditorViewModel(ports, "graph-tester", this));
    }

    public async Task NavigateToMixerV2View()
    {
        MixerMenuItemSelected = false;
        ProAudioStreamsMenuItemSelected = false;
        MetadataMenuItemSelected = false;
        ModuleManagerViewSelected = false;
        FilterGraphBuilderViewSelected = false;
        FilterGraphManagerViewSelected = false;
        CustomControlTesterViewSelected = false;
        MixerManagerViewSelected = false;
        GraphControlTesterViewSelected = false;
        MidiConnectionEditorViewSelected = false;
        MixerViewV2ViewSelected = true;

        var mixerService = Locator.Current
            .GetService<Services.MixerServiceV2.IMixerService>();

        var mixerViewModelService =
            Locator.Current.GetService<IMixerViewModelService>();

        if (mixerService is null || mixerViewModelService is null) return;

        if (mixerService.CurrentMixer is null)
            await mixerService.NewCurrentMixer("Default Mixer");

        var mixer = mixerService.CurrentMixer;
        var mixerView =
            await mixerViewModelService.ConvertCurrentMixerToViewModel(
                "mixer-v2",
                this);

        if (mixerView is not null)
            Router.Navigate.Execute(mixerView);
    }
}