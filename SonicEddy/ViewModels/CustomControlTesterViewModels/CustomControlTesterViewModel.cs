using System;
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

    public bool IsSelected
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public void SwitchSelectedChannelState()
    {
        IsSelected = !IsSelected;
    }

    public void DeleteButtonAction(string param)
    {
        Console.WriteLine($"Delete {param}");
    }
    
    public string? UrlPathSegment { get; }
    public IScreen HostScreen { get; }
    public ViewModelActivator Activator { get; } = new();
}