using Fr.Wireplumber.PInvoke;

namespace Fr.Wireplumber.Model.Objects;

/// <summary>
/// Describes a link from one port to another
/// </summary>
/// <param name="ObjectId">
///     Object id of the link.
/// </param>
/// <param name="ObjectSerial">
///     Object serial of the link.
/// </param>
/// <param name="InputNodeId">
///     Object id of the playback node of the link.
/// </param>
/// <param name="OutputNodeId">
///     Object id of the capture node of the link.
/// </param>
/// <param name="InputPortId">
///     Id of the port of the playback node of the link.
/// </param>
/// <param name="OutputPortId">
///     Id of the port of the capture node of the link.
/// </param>
public record Link(
    ulong ObjectId,
    ulong ObjectSerial,
    ulong InputNodeId,
    ulong OutputNodeId,
    ulong InputPortId,
    ulong OutputPortId
) : IWireplumberObject
{
    /// <summary>
    /// <inheritdoc cref="IWireplumberObject.TriggerDeletedEvent" />
    /// </summary>
    public void TriggerDeletedEvent() => Deleted?.Invoke();

    /// <summary>
    /// <inheritdoc cref="IWireplumberObject.Deleted" />
    /// </summary>
    public event Action? Deleted;

    internal static Link FromWireplumber(WireplumberData data)
    {
        return new(
            data.object_id,
            data.object_serial,
            data.link_input_node,
            data.link_output_node,
            data.link_input_port,
            data.link_output_port
        );
    }
}