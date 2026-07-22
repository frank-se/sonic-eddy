using System.Collections.ObjectModel;

namespace SonicEddy.Controls.MixerControls;

public interface IReturnChannel  :IChannel
{
    bool HasFilter { get; }

    void OnAddFilter(object channel);
    void OnDeleteFilter(object channel);
}