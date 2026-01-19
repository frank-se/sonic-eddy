using ReactiveUI;

namespace SonicEddy.ViewModels.CustomControlTesterViewModels;

public class CustomControlTesterViewModel : ViewModelBase,
    IActivatableViewModel, IRoutableViewModel
{
    public CustomControlTesterViewModel(string? urlPathSegment,
        IScreen hostScreen)
    {
        UrlPathSegment = urlPathSegment;
        HostScreen = hostScreen;
    }

    public string? UrlPathSegment { get; }
    public IScreen HostScreen { get; }
    public ViewModelActivator Activator { get; } = new();
}