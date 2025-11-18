using ReactiveUI;

namespace SonicEddy.ViewModels.MixerViewModels;

public class Stream(
    string name,
    ulong objectSerial,
    double volume,
    string description,
    TargetObject? targetObject,
    double pan,
    bool stereo,
    ulong objectId) : ReactiveObject
{
    private string _description = description;
    private string _name = name;
    private ulong _objectSerial = objectSerial;
    private double _pan = pan;
    private TargetObject? _targetObject = targetObject;
    private double _volume = volume;

    public string DisplayName => $"{Name} ({ObjectId})";
    
    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    public string Description
    {
        get => _description;
        set => this.RaiseAndSetIfChanged(ref _description, value);
    }

    public ulong ObjectSerial
    {
        get => _objectSerial;
        set => this.RaiseAndSetIfChanged(ref _objectSerial, value);
    }
    
    public ulong ObjectId { get; } = objectId;

    public double Volume
    {
        get => _volume;
        set => this.RaiseAndSetIfChanged(ref _volume, value);
    }

    public double Pan
    {
        get => _pan;
        set => this.RaiseAndSetIfChanged(ref _pan, value);
    }

    public bool IsStereo => stereo;

    public double Minimum => 0.0;
    public double Maximum => 1.5;

    public TargetObject? TargetObject
    {
        get => _targetObject;
        set => this.RaiseAndSetIfChanged(ref _targetObject, value);
    }
}