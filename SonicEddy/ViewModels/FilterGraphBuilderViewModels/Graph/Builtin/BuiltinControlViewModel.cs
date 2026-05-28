using ReactiveUI;

namespace SonicEddy.ViewModels.FilterGraphBuilderViewModels.Graph.Builtin;

public class BuiltinControlViewModel : ReactiveObject
{
    private double _value;

    public BuiltinControlViewModel(string name, double value, double min, double max)
    {
        Name = name;
        Min = min;
        Max = max;
        _value = value;
    }

    public string Name { get; }
    public double Min { get; }
    public double Max { get; }

    public double Value
    {
        get => _value;
        set => this.RaiseAndSetIfChanged(ref _value, value);
    }
}
