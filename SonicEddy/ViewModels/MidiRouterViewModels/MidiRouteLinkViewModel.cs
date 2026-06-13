using System;
using System.Windows.Input;
using ReactiveUI;

namespace SonicEddy.ViewModels.MidiRouterViewModels;

public sealed class MidiRouteLinkViewModel(
    Guid routeId,
    string source,
    string target,
    string manipulation,
    string status,
    Action<Guid> delete)
{
    public string Source { get; } = source;
    public string Target { get; } = target;
    public string Manipulation { get; } = manipulation;
    public string Status { get; } = status;
    public ICommand DeleteCommand { get; } =
        ReactiveCommand.Create(() => delete(routeId));
}
