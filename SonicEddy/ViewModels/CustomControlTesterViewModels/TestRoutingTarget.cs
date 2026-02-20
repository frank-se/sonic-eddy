using SonicEddy.Controls.MixerControls;

namespace SonicEddy.ViewModels.CustomControlTesterViewModels;

public class TestRoutingTarget : IRoutingTarget
{
    public required string Name { get; init; }
}