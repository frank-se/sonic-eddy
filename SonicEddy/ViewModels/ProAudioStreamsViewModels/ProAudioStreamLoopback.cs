namespace SonicEddy.ViewModels.ProAudioStreamsViewModels;

public class ProAudioStreamLoopback(
    ProAudioStreamTargetObject sourceTargetObject,
    string name,
    string description,
    ulong leftPortId,
    ulong rightPortId)
{
    public ProAudioStreamTargetObject SourceTargetObject { get; } =
        sourceTargetObject;

    public string Name { get; } = name;
    public string Description { get; } = description;
    public string CaptureNodeName { get; } = $"{name}-capture";
    public string PlaybackNodeName { get; } = $"{name}-playback";
    public ulong LeftPortId { get; } = leftPortId;
    public ulong RightPortId { get; } = rightPortId;
}