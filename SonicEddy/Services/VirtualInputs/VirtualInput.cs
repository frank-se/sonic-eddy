using Fr.Wireplumber.Model.Objects;
using Fr.Wireplumber.Modules.Models;

namespace SonicEddy.Services.VirtualInputs;

public record VirtualInput(
    string Name,
    Node PlaybackNode,
    Port[] Ports,
    LoopbackModule Loopback);