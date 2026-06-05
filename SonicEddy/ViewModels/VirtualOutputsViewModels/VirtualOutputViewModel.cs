using System.Linq;
using SonicEddy.Services.VirtualOutputs;

namespace SonicEddy.ViewModels.VirtualOutputsViewModels;

public sealed class VirtualOutputViewModel
{
    public VirtualOutputViewModel(VirtualOutput virtualOutput)
    {
        VirtualOutput = virtualOutput;
        Ports = string.Join(", ",
            virtualOutput.Ports.Select(port => port.Channel));
    }

    public VirtualOutput VirtualOutput { get; }
    public string Ports { get; }
}
