using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fr.Sonic.Model.Objects;

namespace SonicEddy.Services.VirtualOutputs;

public interface IVirtualOutputService
{
    List<VirtualOutput> VirtualOutputs { get; }

    Task InitializeAsync();

    Task AddVirtualOutput(string name, Node node, Port[] ports);

    Task DeleteVirtualOutput(VirtualOutput virtualOutput);

    event Action<VirtualOutput>? Added;
    event Action<VirtualOutput>? Deleted;
}
