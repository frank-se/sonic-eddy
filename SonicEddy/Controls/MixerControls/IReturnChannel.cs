using System.Collections.ObjectModel;

namespace SonicEddy.Controls.MixerControls;

public interface IReturnChannel  :IChannel
{
    bool HasFilter { get; }

    void OnAddFilter(IOutputChannel channel);
    void OnDeleteFilter(IOutputChannel channel);
}