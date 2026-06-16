using System;
using System.Collections.Generic;
using Fr.Sonic.Modules.Models;

namespace SonicEddy.Services.JackInputPorts;

public record JackInputPort(
    Guid Id,
    string Name,
    string ClientName,
    IReadOnlyList<string> JackPortNames,
    LoopbackModule Loopback);
