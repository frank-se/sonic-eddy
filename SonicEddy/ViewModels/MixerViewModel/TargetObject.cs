using ReactiveUI;

namespace SonicEddy.ViewModels.MixerViewModel;

public class TargetObject(
    string name,
    ulong objectSerial,
    string nick,
    string description) : ReactiveObject
{
    public string Name { get; } = name;
    public ulong ObjectSerial { get; } = objectSerial;
    public string Nick { get; } = nick;
    public string Description { get; } = description;
}