using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fr.Sonic.Model.Objects;

namespace SonicEddy.Services.MidiRouter;

public interface IMidiRouterService
{
    IReadOnlyCollection<MidiRoute> Routes { get; }
    event Action? RoutesChanged;

    Task<MidiRoute> CreateRouteAsync(Port source, Port target,
        MidiManipulationConfig manipulation);

    void DeleteRoute(Guid routeId);
}
