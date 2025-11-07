using ReactiveUI;

namespace SonicEddy.ViewModels.MixerViewModel;

public class Stream(
    string name,
    ulong objectSerial,
    double volume,
    string description,
    TargetObject? targetObject) : ReactiveObject
{
    private string _description = description;
    private string _name = name;
    private ulong _objectSerial = objectSerial;
    private TargetObject? _targetObject = targetObject;
    private double _volume = volume;

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

    public double Volume
    {
        get => _volume;
        set => this.RaiseAndSetIfChanged(ref _volume, value);
    }

    public double Minimum => 0.0;
    public double Maximum => 10.0;

    public TargetObject? TargetObject
    {
        get => _targetObject;
        set => this.RaiseAndSetIfChanged(ref _targetObject, value);
    }
}