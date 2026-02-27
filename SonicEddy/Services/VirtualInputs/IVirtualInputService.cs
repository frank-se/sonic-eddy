using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fr.Wireplumber.Model.Objects;

namespace SonicEddy.Services.VirtualInputs;

public interface IVirtualInputService
{
    List<VirtualInput> VirtualInputs { get; }

    Task AddVirtualInput(string name, Node node, Port[] ports);

    event Action<VirtualInput>? Added;
}