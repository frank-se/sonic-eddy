using System;
using Fr.Sonic.Model.Objects;
using ReactiveUI;
using SonicEddy.ViewModels;

namespace SonicEddy.ViewModels.MidiSyncViewModels;

public sealed class MidiSyncPortViewModel(
    Port port,
    bool receivesSync,
    bool existingLink,
    Action<MidiSyncPortViewModel>? syncChanged = null)
    : ViewModelBase
{
    public Port Port { get; } = port;

    public string Name =>
        Port.Alias ?? Port.Name ?? $"Port {Port.ObjectId}";

    public bool ReceivesSync
    {
        get;
        set
        {
            if (value == field)
                return;

            this.RaiseAndSetIfChanged(ref field, value);
            syncChanged?.Invoke(this);
        }
    } = receivesSync;

    public bool ExistingLink { get; set; } = existingLink;
}
