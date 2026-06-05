namespace Fr.Sonic.Model.Config.ClickSync;

public sealed record ClickSyncConverterConfig(
    Guid Id,
    string Name,
    uint PulsesPerQuarterNote,
    double PulseLengthMs,
    float PulseAmplitude);
