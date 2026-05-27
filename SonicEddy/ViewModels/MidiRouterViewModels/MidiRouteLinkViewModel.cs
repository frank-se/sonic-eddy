using System;
using System.Windows.Input;
using ReactiveUI;

namespace SonicEddy.ViewModels.MidiRouterViewModels;

public sealed class MidiRouteLinkViewModel(
    ulong sourcePortId,
    ulong targetPortId,
    string source,
    string target,
    Action<ulong, ulong> delete)
{
    public string Source { get; } = source;
    public string Target { get; } = target;
    public ICommand DeleteCommand { get; } =
        ReactiveCommand.Create(() => delete(sourcePortId, targetPortId));
}
