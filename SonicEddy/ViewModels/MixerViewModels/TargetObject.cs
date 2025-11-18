using ReactiveUI;

namespace SonicEddy.ViewModels.MixerViewModels;

public class TargetObject(
    string name,
    ulong objectSerial,
    string className,
    string description) : ReactiveObject
{
    public string Name { get; } = name;
    public ulong ObjectSerial { get; } = objectSerial;
    public string Class { get; } = className;
    public string Description { get; } = description;
}