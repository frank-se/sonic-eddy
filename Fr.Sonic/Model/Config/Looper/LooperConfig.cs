namespace Fr.Sonic.Model.Config.Looper;

public record LooperConfig(
    string Name,
    string Description,
    string? CaptureTargetObject,
    string? PlaybackTargetObject,
    string? ArchiveFolderPath = null,
    uint Channels = 2,
    uint MaxRecordSeconds = 300,
    float Mix = 0.0f);
