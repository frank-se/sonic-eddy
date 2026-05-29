using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using SonicEddy.ViewModels.MixerViewModelsV2;

namespace SonicEddy.Controls.MixerControls;

public interface IMasterChannel : IChannel
{
    Services.MixerServiceV2.MasterChannel MasterChannel { get; }

    LooperSectionViewModel Looper { get; }

    ulong ChannelId { get; }

    public bool HasFilter { get; set; }

    public ObservableCollection<IParameter>? FirstPluginParameters { get; }

    public string FirstPluginText { get; set; }

    public ObservableCollection<IParameter>? SecondPluginParameters { get; }

    public string SecondPluginText { get; set; }

    public ObservableCollection<IParameter>? ThirdPluginParameters { get; }

    public string ThirdPluginText { get; set; }

    public ObservableCollection<IRoutingTarget>
        AudioToRoutingTargets { get; }

    public IRoutingTarget? SelectedAudioToRoutingTarget { get; set; }

    public ObservableCollection<FilterChainPresetViewModel> AvailablePresets { get; }
    public ICommand SavePresetCommand { get; }
    public ICommand LoadPresetCommand { get; }

    public Task AddFilterAction();
    public void DeleteFilterAction();
}
