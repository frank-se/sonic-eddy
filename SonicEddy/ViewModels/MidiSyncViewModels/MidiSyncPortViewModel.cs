using Fr.Sonic.Model.Objects;
using ReactiveUI;
using SonicEddy.ViewModels;

namespace SonicEddy.ViewModels.MidiSyncViewModels;

public sealed class MidiSyncPortViewModel(Port port, bool receivesSync)
    : ViewModelBase
{
    public Port Port { get; } = port;

    public string Name =>
        Port.Alias ?? Port.Name ?? $"Port {Port.ObjectId}";

    public bool ReceivesSync
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = receivesSync;

    public bool ExistingLink { get; set; } = receivesSync;
}
