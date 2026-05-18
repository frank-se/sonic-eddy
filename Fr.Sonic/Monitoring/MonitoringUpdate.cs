namespace Fr.Sonic.Monitoring;

public record MonitoringUpdate(
    ulong ObjectSerial,
    float[] Peaks,
    float[] Averages);