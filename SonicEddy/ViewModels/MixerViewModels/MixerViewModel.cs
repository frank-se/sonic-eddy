using System.Collections.Generic;
using System.Collections.ObjectModel;
using ReactiveUI;
using SonicEddy.Services.AppData;

namespace SonicEddy.ViewModels.MixerViewModels;

public class MixerViewModel : ViewModelBase, IActivatableViewModel,
    IRoutableViewModel
{
    public ObservableCollection<ChannelStripViewModel> Channels { get; } = [];

    private IAppDataService _appDataService;

    public MixerViewModel(IAppDataService appDataService,
        string? urlPathSegment, IScreen hostScreen)
    {
        _appDataService = appDataService;
        UrlPathSegment = urlPathSegment;
        HostScreen = hostScreen;
        
        Channels.Add(new(appDataService, 1));
    }

    public string? UrlPathSegment { get; }
    public IScreen HostScreen { get; }
    public ViewModelActivator Activator { get; } = new();
}