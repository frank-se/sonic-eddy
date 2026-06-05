using Fr.Sonic.Model.Objects;
using Fr.Sonic.Modules.Models;

namespace SonicEddy.Services.VirtualOutputs;

public record VirtualOutput(
    string Name,
    Node CaptureNode,
    Port[] Ports,
    LoopbackModule Loopback);
