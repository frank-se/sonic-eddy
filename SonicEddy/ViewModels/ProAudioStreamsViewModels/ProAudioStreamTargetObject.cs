namespace SonicEddy.ViewModels.ProAudioStreamsViewModels;

public class ProAudioStreamTargetObject(
    ulong objectSerial,
    ulong objectId,
    string nodeName,
    string description)
{
    public ulong ObjectSerial { get; } = objectSerial;
    public ulong ObjectId { get; } = objectId;
    public string NodeName { get; } = nodeName;
    public string Description { get; } = description;
}