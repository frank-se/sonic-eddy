using Fr.Wireplumber.Marshalling;
using Fr.Wireplumber.PInvoke;

namespace Fr.Wireplumber.Model.Objects;

/// <summary>
/// The node the port belongs too
/// </summary>
/// <param name="Id">Object Id of the node</param>
public record NodePort(ulong Id);

/// <summary>
/// A pipewire port
/// </summary>
/// <param name="ObjectId">Object Id</param>
/// <param name="ObjectSerial">Object Serial</param>
/// <param name="PortId">
///     Id of the port. This id is relative to the node.
/// </param>
/// <param name="FormatDsp"></param>
/// <param name="Name">Name of the port</param>
/// <param name="Alias">Alias of the port</param>
/// <param name="Group"></param>
/// <param name="Direction">Port direction, <c>in</c>, or <c>out</c></param>
/// <param name="Node"></param>
public record Port(
    ulong ObjectId,
    ulong ObjectSerial,
    ulong PortId,
    string? FormatDsp,
    string? Name,
    string? Alias,
    string? Group,
    string? Direction,
    string? Channel,
    NodePort Node
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
    
    internal static Port FromWireplumber(WireplumberData data)
    {
        return new(
            data.object_id,
            data.object_serial,
            data.port_id,
            data.format_dsp.ConvertToString(),
            data.port_name.ConvertToString(),
            data.port_alias.ConvertToString(),
            data.port_group.ConvertToString(),
            data.port_direction.ConvertToString(),
            data.audio_channel.ConvertToString(),
            new(data.node_id)
        );
    }
}