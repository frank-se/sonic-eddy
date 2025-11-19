using System.Collections.ObjectModel;
using SonicEddy.Services.AppData;

namespace SonicEddy.ViewModels.ProAudioStreamsViewModels;

public class ProAudioStreamsViewModel : ViewModelBase
{
    private readonly IAppDataService _appDataService;

    public ProAudioStreamsViewModel(IAppDataService appDataService)
    {
        _appDataService = appDataService;
        ProAudioStreams.Add(new(
            new(12, 2, "alsa_input.pci-0000_04_00.0.pro-input-0"),
            "pro audio loopback", "Pro Audio Loopback", 10, 11));

        ProAudioStreams.Add(new(
            new(12, 2, "alsa_input.pci-0000_04_00.0.pro-input-0"),
            "pro audio loopback", "Pro Audio Loopback", 12, 13));

        ProAudioStreams.Add(new(
            new(12, 2, "alsa_input.pci-0000_04_00.0.pro-input-0"),
            "pro audio loopback", "Pro Audio Loopback", 0, 1));
    }

    public ObservableCollection<ProAudioStreamLoopback>
        ProAudioStreams { get; } = [];
}