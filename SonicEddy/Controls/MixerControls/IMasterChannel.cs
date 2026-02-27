using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace SonicEddy.Controls.MixerControls;

public interface IMasterChannel : IChannel
{
    Services.MixerServiceV2.MasterChannel MasterChannel { get; }

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

    public Task AddFilterAction();
    public void DeleteFilterAction();
}