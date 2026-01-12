using ReactiveUI;
using SonicEddy.Services.AppData;
using SonicEddy.ViewModels.FilterGraphBuilderViewModels;
using SonicEddy.ViewModels.MetadataViewModels;
using SonicEddy.ViewModels.MixerViewModels;
using SonicEddy.ViewModels.ModuleViewModels;
using SonicEddy.ViewModels.ObjectBrowserViewModels;
using SonicEddy.ViewModels.ProAudioStreamsViewModels;
using Splat;

namespace SonicEddy.ViewModels;

public class MainWindowViewModel : ViewModelBase, IScreen
{
    public RoutingState Router { get; } = new();

    private bool _mixerMenuItemSelected;
    private bool _objectBrowserMenuItemSelected;
    private bool _proAudioStreamsMenuItemSelected;
    private bool _metadataMenuItemSelected;
    private bool _moduleManagerViewSelected;
    private bool _filterGraphBuilderViewSelected;

    public MainWindowViewModel()
    {
        NavigateToMixerAction();
    }

    public bool ProAudioStreamsMenuItemSelected
    {
        get => _proAudioStreamsMenuItemSelected;
        set => this.RaiseAndSetIfChanged(ref _proAudioStreamsMenuItemSelected,
            value);
    }

    public bool MixerMenuItemSelected
    {
        get => _mixerMenuItemSelected;
        set => _mixerMenuItemSelected =
            this.RaiseAndSetIfChanged(ref _mixerMenuItemSelected, value);
    }

    public bool ObjectBrowserMenuItemSelected
    {
        get => _objectBrowserMenuItemSelected;
        set => this.RaiseAndSetIfChanged(ref _objectBrowserMenuItemSelected,
            value);
    }

    public bool MetadataMenuItemSelected
    {
        get => _metadataMenuItemSelected;
        set => this.RaiseAndSetIfChanged(ref _metadataMenuItemSelected, value);
    }

    public bool ModuleManagerViewSelected
    {
        get => _moduleManagerViewSelected;
        set => this.RaiseAndSetIfChanged(ref _moduleManagerViewSelected, value);
    }

    public bool FilterGraphBuilderViewSelected
    {
        get => _filterGraphBuilderViewSelected;
        set => this.RaiseAndSetIfChanged(ref _filterGraphBuilderViewSelected,
            value);
    }

    public void NavigateToMixerAction()
    {
        MixerMenuItemSelected = true;
        ObjectBrowserMenuItemSelected = false;
        ProAudioStreamsMenuItemSelected = false;
        MetadataMenuItemSelected = false;
        ModuleManagerViewSelected = false;
        FilterGraphBuilderViewSelected = false;
        var appDataService = Locator.Current.GetService<IAppDataService>();
        Router.Navigate.Execute(new MixerViewModel(appDataService!, "mixer",
            this));
    }

    public void NavigateToObjectBrowserAction()
    {
        MixerMenuItemSelected = false;
        ObjectBrowserMenuItemSelected = true;
        ProAudioStreamsMenuItemSelected = false;
        MetadataMenuItemSelected = false;
        ModuleManagerViewSelected = false;
        FilterGraphBuilderViewSelected = false;
        var appDataService = Locator.Current.GetService<IAppDataService>();
        Router.Navigate.Execute(
            new ObjectBrowserViewModel(appDataService!, "object-browser",
                this));
    }

    public void NavigateToProAudioStreamsAction()
    {
        MixerMenuItemSelected = false;
        ObjectBrowserMenuItemSelected = false;
        ProAudioStreamsMenuItemSelected = true;
        MetadataMenuItemSelected = false;
        ModuleManagerViewSelected = false;
        FilterGraphBuilderViewSelected = false;
        var appDataService = Locator.Current.GetService<IAppDataService>();
        Router.Navigate.Execute(new ProAudioStreamsViewModel(appDataService!,
            "pro-audio-streams", this));
    }

    public void NavigateToMetadataView()
    {
        MixerMenuItemSelected = false;
        ObjectBrowserMenuItemSelected = false;
        ProAudioStreamsMenuItemSelected = false;
        MetadataMenuItemSelected = true;
        ModuleManagerViewSelected = false;
        FilterGraphBuilderViewSelected = false;
        var appDataService = Locator.Current.GetService<IAppDataService>();
        Router.Navigate.Execute(new MetadataViewModel(appDataService!,
            "metadata", this));
    }

    public void NavigateToModuleManagerView()
    {
        MixerMenuItemSelected = false;
        ObjectBrowserMenuItemSelected = false;
        ProAudioStreamsMenuItemSelected = false;
        MetadataMenuItemSelected = false;
        ModuleManagerViewSelected = true;
        FilterGraphBuilderViewSelected = false;
        var appDataService = Locator.Current.GetService<IAppDataService>();
        Router.Navigate.Execute(new ModuleManagerViewModel(appDataService!,
            "module-manager", this));
    }

    public void NavigateToFilterGraphBuilderView()
    {
        MixerMenuItemSelected = false;
        ObjectBrowserMenuItemSelected = false;
        ProAudioStreamsMenuItemSelected = false;
        MetadataMenuItemSelected = false;
        ModuleManagerViewSelected = false;
        FilterGraphBuilderViewSelected = true;
        var appDataService = Locator.Current.GetService<IAppDataService>();
        Router.Navigate.Execute(new FilterGraphBuilderViewModel(appDataService!,
            "filter-graph-builder", this));
    }
}