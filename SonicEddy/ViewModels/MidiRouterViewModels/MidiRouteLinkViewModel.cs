using System;
using System.Windows.Input;
using Fr.Sonic.Model.Objects;
using ReactiveUI;

namespace SonicEddy.ViewModels.MidiRouterViewModels;

public sealed class MidiRouteLinkViewModel(
    Link link,
    string source,
    string target,
    Action<Link> delete)
{
    public string Source { get; } = source;
    public string Target { get; } = target;
    public ICommand DeleteCommand { get; } =
        ReactiveCommand.Create(() => delete(link));
}
