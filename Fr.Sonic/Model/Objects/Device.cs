using Fr.Sonic.Marshalling;
using Fr.Sonic.PInvoke;

// ReSharper disable NotAccessedPositionalProperty.Global

namespace Fr.Sonic.Model.Objects;

/// <summary>
/// Parameters from the alsa card profile (APC) of the alsa device.
/// </summary>
/// <param name="AutoPort">
///     Automatically set the highest priority port. Normally disabled, because
///     the session manager handles that.
/// </param>
/// <param name="AutoProfile">
///     Automatically select the best profile for the device. Normally disabled, because
///     the session manager handles that.
/// </param>
/// <param name="ProChannels">
///     The number of channels to use when probing the pro-audio profile.
///     Normally the maximum number of ports will be selected, this helps in
///     using fewer ports, e.g. in order to use a higher sample rate.
/// </param>
/// <param name="ProbeRate">
///     Sets the sample rate for probing the device, and collecting the
///     profiles, and ports.
/// </param>
public record AcpDevice(
    bool AutoPort,
    bool AutoProfile,
    ulong? ProChannels,
    ulong? ProbeRate
);

/// <summary>
/// Information about the Alsa device
/// </summary>
/// <param name="DisableMixerPath"></param>
/// <param name="IgnoreDb"></param>
/// <param name="Path"></param>
/// <param name="SoftMixer"></param>
/// <param name="SplitEnable"></param>
/// <param name="UseUcm"></param>
public record AlsaDevice(
    bool DisableMixerPath,
    bool IgnoreDb,
    string? Path,
    bool SoftMixer,
    bool SplitEnable,
    bool UseUcm
);

/// <summary>
/// Product information about the device.
/// </summary>
/// <param name="Id"></param>
/// <param name="Name"></param>
public record ProductDevice(
    string? Id,
    string? Name
);

/// <summary>
/// Vendor information about the device.
/// </summary>
/// <param name="Id"></param>
/// <param name="Name"></param>
public record VendorDevice(
    string? Id,
    string? Name
);

/// <summary>
/// Information about the API used for this device
/// </summary>
/// <param name="Acp">Alsa card profile</param>
/// <param name="Alsa">Alsa information</param>
public record ApiDevice(
    AcpDevice Acp,
    AlsaDevice Alsa
);

/// <summary>
/// The client the device is attached to
/// </summary>
/// <param name="Id">Object Id of the client</param>
public record ClientDevice(ulong Id);

/// <summary>
/// A pipewire device. Sound cards, bluetooth devices, cameras, etc.
/// </summary>
/// <param name="ObjectId">Object Id</param>
/// <param name="ObjectSerial">Object Serial</param>
/// <param name="Class">Class of the device</param>
/// <param name="Description">
///     Description of the device, normally used in UIs
/// </param>
/// <param name="FormFactor"></param>
/// <param name="Icon"></param>
/// <param name="IconName"></param>
/// <param name="IntendedRoles"></param>
/// <param name="Name">Name of the device</param>
/// <param name="Nick">Nickname for the device</param>
/// <param name="Profile">Active device profile</param>
/// <param name="ProfileSet">Available device profiles</param>
/// <param name="Serial"></param>
/// <param name="SubSystem"></param>
/// <param name="Api"></param>
/// <param name="Product"></param>
/// <param name="Vendor"></param>
/// <param name="Client">Client id for the device</param>
/// <param name="Nodes"></param>
public record Device(
    ulong ObjectId,
    ulong ObjectSerial,
    string? Class,
    string? Description,
    string? FormFactor,
    string? Icon,
    string? IconName,
    string? IntendedRoles,
    string? Name,
    string? Nick,
    string? Profile,
    string? ProfileSet,
    string? Serial,
    string? SubSystem,
    ApiDevice Api,
    ProductDevice Product,
    VendorDevice Vendor,
    ClientDevice Client,
    Task<List<Node>> Nodes
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

    /// <summary>
    /// The event is trigger when the node list changed, generally because a
    /// node is added to the node list.
    /// </summary>
    public event Action<List<Node>>? NodesListChanged;

    internal void TriggerNodesListChangedEvent() =>
        NodesListChanged?.Invoke(Nodes.Result);

    internal static Device FromWireplumber(WireplumberData data,
        Task<List<Node>> nodes)
    {
        return new(
            data.object_id,
            data.object_serial,
            data.device_class.ConvertToString(),
            data.device_description.ConvertToString(),
            data.device_form_factor.ConvertToString(),
            data.device_icon.ConvertToString(),
            data.device_icon_name.ConvertToString(),
            data.device_intended_roles.ConvertToString(),
            data.device_name.ConvertToString(),
            data.device_nick.ConvertToString(),
            data.device_profile.ConvertToString(),
            data.device_profile_set.ConvertToString(),
            data.device_serial.ConvertToString(),
            data.device_subsystem.ConvertToString(),
            new(
                new(
                    data.api_acp_auto_port,
                    data.api_acp_auto_profile,
                    data.api_acp_pro_channels,
                    data.api_acp_probe_rate
                ),
                new(
                    data.api_alsa_disable_mixer_path,
                    data.api_alsa_ignore_db,
                    data.api_alsa_path.ConvertToString(),
                    data.api_alsa_soft_mixer,
                    data.api_alsa_split_enable,
                    data.api_alsa_use_ucm
                )
            ),
            new(
                data.device_product_id.ConvertToString(),
                data.device_product_name.ConvertToString()
            ),
            new(
                data.device_vendor_id.ConvertToString(),
                data.device_vendor_name.ConvertToString()
            ),
            new(data.client_id),
            nodes);
    }
}