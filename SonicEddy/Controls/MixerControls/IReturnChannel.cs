using System.Collections.ObjectModel;

namespace SonicEddy.Controls.MixerControls;

public interface IReturnChannel  :IChannel
{
    bool HasFilter { get; }

    ObservableCollection<IRoutingTarget> RoutingTargets { get; }
    IRoutingTarget? SelectedRoutingTarget { get; set; }

    void OnAddFilter(IOutputChannel channel);
    void OnDeleteFilter(IOutputChannel channel);
}