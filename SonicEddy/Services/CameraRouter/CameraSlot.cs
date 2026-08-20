namespace SonicEddy.Services.CameraRouter;

public sealed record CameraSlot(int Index, string? SourceNodeName, bool Connected);
