using ReactiveUI;
using SonicEddy.ViewModels.MixerViewModels;
using SonicEddy.ViewModels.ObjectBrowserViewModels;
using SonicEddy.ViewModels.ProAudioStreamsViewModels;

namespace SonicEddy.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private ViewModelBase? _currentPageViewModel;

    private bool _mixerMenuItemSelected;
    private bool _objectBrowserMenuItemSelected;
    private bool _proAudioStreamsMenuItemSelected;

    public MainWindowViewModel()
    {
        NavigateToMixerAction();
    }

    public ViewModelBase? CurrentPageViewModel
    {
        get => _currentPageViewModel;
        set => this.RaiseAndSetIfChanged(ref _currentPageViewModel, value);
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

    public void NavigateToMixerAction()
    {
        MixerMenuItemSelected = true;
        ObjectBrowserMenuItemSelected = false;
        ProAudioStreamsMenuItemSelected = false;
        CurrentPageViewModel = new MixerViewModel();
    }

    public void NavigateToObjectBrowserAction()
    {
        MixerMenuItemSelected = false;
        ObjectBrowserMenuItemSelected = true;
        ProAudioStreamsMenuItemSelected = false;
        CurrentPageViewModel = new ObjectBrowserViewModel();
    }

    public void NavigateToProAudioStreamsAction()
    {
        MixerMenuItemSelected = false;
        ObjectBrowserMenuItemSelected = false;
        ProAudioStreamsMenuItemSelected = true;
        CurrentPageViewModel = new ProAudioStreamsViewModel();
    }
}