using System.Runtime.InteropServices;
using Fr.Wireplumber.Model.Objects;
using Fr.Wireplumber.PInvoke;

namespace Fr.Wireplumber.Model.Props;

/// <summary>
/// Properties of a pipewire node
/// </summary>
/// <param name="ObjectId">Object Id of the owner</param>
/// <param name="ObjectSerial">Object Serial of the owner</param>
/// <param name="Type">Type of the owner</param>
/// <param name="Volume">Volume</param>
/// <param name="Mute">Mute state</param>
/// <param name="MonitorMute">Should mute be monitored</param>
/// <param name="SoftMute">Soft mute</param>
/// <param name="Channels">Description of all channels</param>
public record Properties(
    ulong ObjectId,
    ulong ObjectSerial,
    wireplumber_object_type Type,
    float Volume,
    bool Mute,
    bool MonitorMute,
    bool SoftMute,
    List<Channel> Channels
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

    internal static Properties FromPipewireProps(PipewirePropsData propsData)
    {
        return new(
            propsData.object_id,
            propsData.object_serial,
            propsData.type,
            propsData.volume,
            propsData.mute,
            propsData.monitor_mute,
            propsData.soft_mute,
            ParseChannelData(propsData));
    }

    private static List<Channel> ParseChannelData(PipewirePropsData propsData)
    {
        if (propsData.channel_volumes_size == 0) return [];

        var sizeOfFloat = Marshal.SizeOf<float>();
        var sizeOfUnsignedInt32 = Marshal.SizeOf<uint>();

        List<Channel> channels = [];
        for (uint i = 0; i < propsData.channel_volumes_size; i++)
        {
            var floatOffset = i * sizeOfFloat;

            var currentVolumePtr =
                new IntPtr(propsData.channel_volumes.ToInt64() + floatOffset);

            var currentMonitorVolumePtr =
                new IntPtr(propsData.monitor_volumes.ToInt64() + floatOffset);

            var currentSoftVolumePtr =
                new IntPtr(propsData.soft_volumes.ToInt64() + floatOffset);

            var currentChannelMapPtr =
                new IntPtr(propsData.channel_map.ToInt64() +
                           i * sizeOfUnsignedInt32);

            channels.Add(new(
                ReadFloat(currentVolumePtr),
                ReadFloat(currentMonitorVolumePtr),
                ReadFloat(currentSoftVolumePtr),
                (ChannelNameEnum)ReadUInt32(currentChannelMapPtr)));
        }

        return channels;
    }

    private static float ReadFloat(IntPtr startPtr)
    {
        var floatBytes = new byte[4];
        Marshal.Copy(startPtr, floatBytes, 0, 4);
        return BitConverter.ToSingle(floatBytes, 0);
    }

    private static uint ReadUInt32(IntPtr startPtr)
    {
        var uintBytes = new byte[4];
        Marshal.Copy(startPtr, uintBytes, 0, 4);
        return BitConverter.ToUInt32(uintBytes, 0);
    }
}