using System.Collections.ObjectModel;
using System.Threading.Tasks;
using DynamicData;
using ReactiveUI;
using SonicEddy.Services.MixerData;
using Mixer = SonicEddy.Contracts.Mixers.Mixer;

namespace SonicEddy.ViewModels.MixerManagerViewModels;

public class MixerManagerViewModel : ViewModelBase, IActivatableViewModel,
    IRoutableViewModel
{
    private readonly IMixerService _mixerService;

    public ObservableCollection<Mixer> Mixers { get; } = [];

    public MixerManagerViewModel(string? urlPathSegment, IScreen hostScreen,
        IMixerService mixerService)
    {
        UrlPathSegment = urlPathSegment;
        HostScreen = hostScreen;
        _mixerService = mixerService;
        _ = GetMixers();
    }

    private async Task GetMixers()
    {
        var mixers = await _mixerService.GetAllMixers();
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            Mixers.Clear();
            Mixers.AddRange(mixers);
        });
    }

    public Task Activate(Mixer mixer) =>
        _mixerService.RestoreMixer(mixer.Id);

    public void Delete(Mixer mixer) => _mixerService.DeleteMixer(mixer.Id);

    public ViewModelActivator Activator { get; } = new();
    public string? UrlPathSegment { get; }
    public IScreen HostScreen { get; }
}