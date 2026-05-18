namespace Fr.Sonic.Model.Props;

/// <summary>
/// Channel information
/// </summary>
/// <param name="Volume">Volume of the channel</param>
/// <param name="SoftVolume">Soft-volume of the channel</param>
/// <param name="MonitorVolume">Should monitor outs monitor volume?</param>
/// <param name="ChannelName">Name of the channel</param>
public record Channel(
    float Volume,
    float SoftVolume,
    float MonitorVolume,
    ChannelNameEnum ChannelName);