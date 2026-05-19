using Fr.Sonic.Model;
using Fr.Sonic.Model.Objects;
using ReactiveUI;

namespace SonicEddy.ViewModels.ObjectDetailsViewModels;

public class PortDetailsViewModel(
    ulong objectId,
    ulong objectSerial,
    ulong portId,
    string formatDsp,
    string name,
    string alias,
    string group,
    string direction,
    ulong nodeId)
    : ObjectDetailsViewModelBase(objectId, objectSerial, "Port"),
        IActivatableViewModel
{
    public ulong PortId => portId;
    public string FormatDsp => formatDsp;
    public string Name => name;
    public string Alias => alias;
    public string Group => group;
    public string Direction => direction;
    public ulong NodeId => nodeId;

    public ViewModelActivator Activator { get; } = new();

    public static PortDetailsViewModel FromPort(Port port)
    {
        return new(
            port.ObjectId,
            port.ObjectSerial,
            port.PortId,
            port.FormatDsp ?? string.Empty,
            port.Name ?? string.Empty,
            port.Alias ?? string.Empty,
            port.Group ?? string.Empty,
            port.Direction ?? string.Empty,
            port.Node.Id);
    }
}