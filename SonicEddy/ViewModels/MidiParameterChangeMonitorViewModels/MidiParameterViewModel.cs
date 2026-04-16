using System;
using ReactiveUI;

namespace SonicEddy.ViewModels.MidiParameterChangeMonitorViewModels;

public class MidiParameterViewModel : ReactiveObject
{
    public MidiParameterViewModel(ulong objectId, string nodeName, string name,
        float currentValue, float targetValue, bool catchingUp)
    {
        ObjectId = objectId;
        NodeName = nodeName;
        Name = name;
        CurrentValue = currentValue;
        TargetValue = targetValue;
        CatchingUp = catchingUp;
    }

    public string NodeName
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string Name
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public ulong ObjectId
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public float CurrentValue
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public float TargetValue
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool CatchingUp
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public DateTime UpdateTime
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }
}