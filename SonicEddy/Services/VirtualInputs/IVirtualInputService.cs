using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fr.Sonic.Model.Objects;

namespace SonicEddy.Services.VirtualInputs;

public interface IVirtualInputService
{
    List<VirtualInput> VirtualInputs { get; }

    Task InitializeAsync();

    Task AddVirtualInput(string name, Node node, Port[] ports);

    Task DeleteVirtualInput(VirtualInput virtualInput);

    event Action<VirtualInput>? Added;
}
