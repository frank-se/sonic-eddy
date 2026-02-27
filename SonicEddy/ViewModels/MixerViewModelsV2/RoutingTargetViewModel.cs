using SonicEddy.Controls.MixerControls;

namespace SonicEddy.ViewModels.MixerViewModelsV2;

public class RoutingTargetViewModel(string name, IChannel channel)
    : IRoutingTarget
{
    public string Name => name;
    public IChannel Channel => channel;
}