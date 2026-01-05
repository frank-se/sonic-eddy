using ReactiveUI;
using SonicEddy.Services.AppData;
using SonicEddy.ViewModels.MetadataViewModels;
using SonicEddy.ViewModels.MixerViewModels;
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

    public void NavigateToMixerAction()
    {
        MixerMenuItemSelected = true;
        ObjectBrowserMenuItemSelected = false;
        ProAudioStreamsMenuItemSelected = false;
        MetadataMenuItemSelected = false;
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
        var appDataService = Locator.Current.GetService<IAppDataService>();
        Router.Navigate.Execute(new MetadataViewModel(appDataService!,
            "metadata", this));
    }
}