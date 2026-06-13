using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fr.Sonic.Model.Objects;
using SonicEddy.Contracts.MidiRouter;

namespace SonicEddy.Services.MidiRouter;

public interface IMidiRouterService
{
    IReadOnlyCollection<MidiRoute> Routes { get; }
    IReadOnlyCollection<MidiRouteConfig> ConfiguredRoutes { get; }
    event Action? RoutesChanged;

    Task InitializeAsync();

    Task<MidiRoute> CreateRouteAsync(Port source, Port target,
        MidiManipulationConfig manipulation);

    void DeleteRoute(Guid routeId);
}
