namespace Fr.Sonic.Model.Config.Ducker;

public record DuckerConfig(
    string Name,
    string Description = "Sonic Eddy ducker",
    string? CaptureTargetObject = null,
    string? PlaybackTargetObject = null);
