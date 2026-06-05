using System;
using Fr.Sonic.Model.Objects;
using ReactiveUI;
using SonicEddy.Services.ClickSync;

namespace SonicEddy.ViewModels.ClickSyncViewModels;

public sealed class ClickSyncPortViewModel(
    Port port,
    bool selected,
    bool linked,
    Action<ClickSyncPortViewModel> changed)
    : ViewModelBase
{
    public Port Port { get; } = port;

    public string Name =>
        Port.Alias ?? Port.Name ?? $"Port {Port.ObjectId}";

    public bool Selected
    {
        get;
        set
        {
            if (value == field) return;
            this.RaiseAndSetIfChanged(ref field, value);
            changed(this);
        }
    } = selected;

    public bool Linked { get; } = linked;
}
