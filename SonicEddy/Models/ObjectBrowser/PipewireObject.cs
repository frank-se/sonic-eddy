using System.Collections.ObjectModel;
using Fr.Sonic.Model.Objects;

namespace SonicEddy.Models.ObjectBrowser;

public enum PipewireObjectType
{
    Client,
    Node,
    Device,
    Port,
    Link
}

public class PipewireObject
{
    public required string Name { get; set; }
    public required ulong ObjectSerial { get; init; }
    public required ulong ObjectId { get; init; }
    public required ObservableCollection<PipewireObject> Children { get; set; }
    public required PipewireObjectType Type { get; init; }

    public static PipewireObject FromClient(Client client)
    {
        return new()
        {
            Name = $"Client {client.ObjectId} - {client.Application.Name}",
            ObjectId = client.ObjectId,
            ObjectSerial = client.ObjectSerial,
            Children = [],
            Type = PipewireObjectType.Client
        };
    }

    public static PipewireObject FromPort(Port port)
    {
        return new()
        {
            Name = $"Port {port.ObjectId} - {port.Name}",
            ObjectId = port.ObjectId,
            ObjectSerial = port.ObjectSerial,
            Children = [],
            Type = PipewireObjectType.Port
        };
    }

    public static PipewireObject FromDevice(Device device)
    {
        return new()
        {
            Name = $"Device {device.ObjectId} - {device.Name}",
            ObjectId = device.ObjectId,
            ObjectSerial = device.ObjectSerial,
            Children = [],
            Type = PipewireObjectType.Device
        };
    }

    public static PipewireObject FromNode(Node node)
    {
        return new()
        {
            Name = $"Node {node.ObjectId} - {node.Name}",
            ObjectId = node.ObjectId,
            ObjectSerial = node.ObjectSerial,
            Children = [],
            Type = PipewireObjectType.Node
        };
    }
}