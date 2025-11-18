namespace SonicEddy.ViewModels.ProAudioStreamsViewModels;

public class ProAudioStreamTargetObject(
    ulong objectSerial,
    ulong objectId,
    string nodeName)
{
    public ulong ObjectSerial { get; } = objectSerial;
    public ulong ObjectId { get; } = objectId;
    public string NodeName { get; } = nodeName;
}