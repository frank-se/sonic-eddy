using System.Collections.Concurrent;
using Fr.Wireplumber.Model.Objects;

namespace Fr.Wireplumber.Registries.Devices;

/// <summary>
/// Provides access to pipewire devices
/// </summary>
public class DeviceRegistry : Registry<Device, DeviceTaskCompletionSources,
    DeviceChangeType>
{
    internal void UpdateNodesList(ulong objectSerial, List<Node> nodes) =>
        UpdateByObjectSerial(objectSerial,
            old => old with
            {
                Nodes = Task.FromResult(nodes)
            },
            tcs => tcs.InitialNodesTaskCompletionSource.TrySetResult(nodes),
            device => device.TriggerNodesListChangedEvent(),
            DeviceChangeType.NodesList
        );

    internal void AddToNodeListByObjectId(ulong objectId, Node node) =>
        UpdateByObjectId(objectId,
            old =>
            {
                if (!old.Nodes.IsCompleted) return old;

                var nodes = old.Nodes.Result;
                nodes.Add(node);
                return old with
                {
                    Nodes = Task.FromResult(nodes)
                };
            },
            tcs => tcs.InitialNodesTaskCompletionSource.TrySetResult([node]),
            device => device.TriggerNodesListChangedEvent(),
            DeviceChangeType.NodesList);
}