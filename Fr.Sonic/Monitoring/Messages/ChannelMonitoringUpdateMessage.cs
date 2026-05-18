namespace Fr.Sonic.Monitoring.Messages;

public record ChannelMonitoringUpdateMessage(
    ulong ObjectSerial,
    float LeftPeak,
    float RightPeak,
    float LeftAverage,
    float RightAverage);