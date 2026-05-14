using Fr.Wireplumber.Model.Objects;

namespace Fr.Wireplumber.Registries.Devices;

/// <summary>
/// Task completion sources for device data
/// </summary>
/// <param name="InitialNodesTaskCompletionSource"></param>
public record DeviceTaskCompletionSources(
    TaskCompletionSource<List<Node>> InitialNodesTaskCompletionSource)
{
    internal DeviceTaskCompletionSources() : this(
        new TaskCompletionSource<List<Node>>())
    {
    }
};