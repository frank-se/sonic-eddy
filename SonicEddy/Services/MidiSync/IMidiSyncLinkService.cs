using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fr.Sonic.Model.Objects;

namespace SonicEddy.Services.MidiSync;

public interface IMidiSyncLinkService
{
    event Action? Changed;

    bool IsSyncOutputAvailable { get; }

    Task InitializeAsync();

    IReadOnlyCollection<MidiSyncPortState> GetPorts();

    void SetReceivesSync(Port port, bool receivesSync);
}
