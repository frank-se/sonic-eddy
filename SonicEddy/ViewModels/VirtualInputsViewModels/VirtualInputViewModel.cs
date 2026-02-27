using System.Linq;
using SonicEddy.Services.VirtualInputs;

namespace SonicEddy.ViewModels.VirtualInputsViewModels;

public class VirtualInputViewModel
{
    public VirtualInputViewModel(VirtualInput virtualInput)
    {
        VirtualInput = virtualInput;
        Ports = string.Join(", ", virtualInput.Ports.Select(p => p.Channel));
    }

    public VirtualInput VirtualInput { get; }
    public string Ports { get; }
}