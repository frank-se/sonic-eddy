using System;
using Fr.Sonic.Model.Objects;
using Fr.Sonic.Modules.Models;

namespace SonicEddy.Services.RecordingPickUp;

public record RecordingPickUp(
    Guid Id,
    string Name,
    bool IsActive,
    double Trim,
    Node SourceNode,
    Node DestNode,
    LoopbackModule Loopback);
