namespace Fr.Pw.Monitoring.Monitoring;

public record MonitoringUpdate(
    ulong ObjectSerial,
    float[] Peaks,
    float[] Averages);