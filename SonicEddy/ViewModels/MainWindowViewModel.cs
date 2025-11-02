using ReactiveUI;

namespace SonicEddy.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private ViewModelBase? _currentPageViewModel;

    public ViewModelBase? CurrentPageViewModel
    {
        get => _currentPageViewModel;
        set => this.RaiseAndSetIfChanged(ref _currentPageViewModel, value);
    }
    
    private bool _mixerMenuItemSelected = true;
    private bool _objectBrowserMenuItemSelected;

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
        CurrentPageViewModel = new MixerViewModel();
    }

    public void NavigateToObjectBrowserAction()
    {
        MixerMenuItemSelected = false;
        ObjectBrowserMenuItemSelected = true;
        CurrentPageViewModel = new ObjectBrowserViewModel();
    }
}