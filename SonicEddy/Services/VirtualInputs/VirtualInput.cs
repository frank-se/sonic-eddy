using Fr.Sonic.Model.Objects;
using Fr.Sonic.Modules.Models;

namespace SonicEddy.Services.VirtualInputs;

public record VirtualInput(
    string Name,
    Node PlaybackNode,
    Port[] Ports,
    LoopbackModule Loopback);