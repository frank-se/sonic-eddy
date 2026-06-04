using Fr.Sonic.Model.Objects;

namespace SonicEddy.Services.MidiSync;

public sealed record MidiSyncPortState(
    Port Port,
    bool ReceivesSync,
    bool ExistingLink);
