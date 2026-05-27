using System.Collections.ObjectModel;
using System.Threading.Tasks;
using SonicEddy.ViewModels.MixerViewModelsV2;

namespace SonicEddy.Controls.MixerControls;

public interface IChannelStrip : IChannel
{
    Services.MixerServiceV2.ChannelStrip ChannelStrip { get; }

    LooperSectionViewModel Looper { get; }
    
    ulong ChannelId { get; }

    public IRoutingTarget? SelectedAudioFromRoutingTarget { get; set; }
    
    bool IsSendMidiControlled { get; set; }
    
    public double Send1Trim { get; set; }

    public double Send2Trim { get; set; }

    public double Send3Trim { get; set; }

    public double Send4Trim { get; set; }

    public bool HasFilter { get; set; }
    
    public bool IsFilterMidiControlled { get; set; }

    public ObservableCollection<IRoutingTarget>
        AudioFromRoutingTargets { get; }

    public ObservableCollection<IParameter>? FirstPluginParameters { get; }
    
    public bool FirstPluginSelectedForMidi { get; set; }

    public string FirstPluginText { get; set; }

    public ObservableCollection<IParameter>? SecondPluginParameters { get; }

    public bool SecondPluginSelectedForMidi { get; set; }
    
    public string SecondPluginText { get; set; }

    public ObservableCollection<IParameter>? ThirdPluginParameters { get; }

    public bool ThirdPluginSelectedForMidi { get; set; }
    
    public string ThirdPluginText { get; set; }

    public ObservableCollection<IRoutingTarget>
        AudioToRoutingTargets { get; }

    public IRoutingTarget? SelectedAudioToRoutingTarget { get; set; }

    void SetMidiControlledSectionId(ulong sectionId);
    
    public void DeleteAction();
    public Task AddFilterAction();
    public void DeleteFilterAction();
}
