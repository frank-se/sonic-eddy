using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using SonicEddy.ViewModels.MixerViewModelsV2;

namespace SonicEddy.Controls.MixerControls;

public interface IMicChannel : IChannel
{
    Services.MixerServiceV2.MicChannel MicChannel { get; }

    ulong ChannelId { get; }

    public bool HasFilter { get; set; }

    public bool IsFilterMidiControlled { get; set; }

    public bool FirstPluginSelectedForMidi { get; }
    public bool SecondPluginSelectedForMidi { get; }
    public bool ThirdPluginSelectedForMidi { get; }

    public ObservableCollection<IParameter>? FirstPluginParameters { get; }

    public string FirstPluginText { get; set; }

    public ObservableCollection<IParameter>? SecondPluginParameters { get; }

    public string SecondPluginText { get; set; }

    public ObservableCollection<IParameter>? ThirdPluginParameters { get; }

    public string ThirdPluginText { get; set; }

    public ObservableCollection<IRoutingTarget>
        AudioFromRoutingTargets { get; }

    public IRoutingTarget? SelectedAudioFromRoutingTarget { get; set; }

    public ObservableCollection<FilterChainPresetViewModel> AvailablePresets { get; }
    public ICommand SavePresetCommand { get; }
    public ICommand LoadPresetCommand { get; }

    public Task AddFilterAction();
    public void DeleteFilterAction();
}
