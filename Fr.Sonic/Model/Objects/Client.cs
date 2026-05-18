using Fr.Sonic.Marshalling;
using Fr.Sonic.PInvoke;
// ReSharper disable NotAccessedPositionalProperty.Global

namespace Fr.Sonic.Model.Objects;

/// <summary>
/// The application of a pipewire client.
/// </summary>
/// <param name="Name">Name of the application</param>
public record ApplicationClient(string? Name);

/// <summary>
/// Security information about the client. These are set by pipewire, and can be
/// trusted.
/// </summary>
/// <param name="Gid">Linux group id</param>
/// <param name="Pid">Linux process id</param>
/// <param name="Uid">Linux User id</param>
/// <param name="Socket"></param>
public record SecurityClient(
    ulong Gid,
    ulong Pid,
    ulong Uid,
    string? Socket
);

/// <summary>
/// Pipewire client information. These are set by pipewire and can be trusted.
/// </summary>
/// <param name="Security">Linux security information</param>
/// <param name="Access">Access level</param>
/// <param name="ClientAccess"></param>
/// <param name="Protocol">The protocol the client used to connect</param>
public record PipewireClient(
    SecurityClient Security,
    string? Access,
    string? ClientAccess,
    string? Protocol
);

/// <summary>
/// A pipewire client.
/// </summary>
/// <param name="ObjectId">Object Id</param>
/// <param name="ObjectSerial">Object Serial</param>
/// <param name="Application">Information about the application</param>
/// <param name="Pipewire">Pipewire information. Set by pipewire</param>
public record Client(
    ulong ObjectId,
    ulong ObjectSerial,
    ApplicationClient Application,
    PipewireClient Pipewire
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
    
    internal static Client FromWireplumber(WireplumberData data)
    {
        return new(
            data.object_id,
            data.object_serial,
            new(data.application_name.ConvertToString()),
            new(
                new(
                    data.pipewire_sec_pid,
                    data.pipewire_sec_gid,
                    data.pipewire_sec_uid,
                    data.pipewire_sec_socket.ConvertToString()
                ),
                data.pipewire_access.ConvertToString(),
                data.pipewire_client_access.ConvertToString(),
                data.pipewire_protocol.ConvertToString()
            )
        );
    }
}