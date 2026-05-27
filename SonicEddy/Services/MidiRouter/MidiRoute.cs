using System;
using System.Collections.Generic;
using Fr.Sonic.Model.Objects;
using Fr.Sonic.Modules.Models;

namespace SonicEddy.Services.MidiRouter;

public sealed record MidiRoute(
    Guid Id,
    Port Source,
    Port Target,
    MidiManipulationConfig Manipulation,
    MidiManipulator? Manipulator,
    IReadOnlyList<ulong> OwnedLinkIds);
