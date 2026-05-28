using Fr.Sonic.Model.Lv2;
using ReactiveUI;

namespace SonicEddy.ViewModels.FilterGraphBuilderViewModels.Graph.Lv2;

public class Lv2ControlViewModel : ReactiveObject
{
    private double _value;

    public Lv2ControlViewModel(PortDescription port)
    {
        Symbol = port.Symbol;
        DisplayName = port.Name;
        Min = port.Minimum ?? 0f;
        Max = port.Maximum ?? 1f;
        if (Min >= Max) Max = Min + 1.0;
        _value = port.Default ?? Min;
    }

    public string Symbol { get; }
    public string DisplayName { get; }
    public double Min { get; }
    public double Max { get; }

    public double Value
    {
        get => _value;
        set => this.RaiseAndSetIfChanged(ref _value, value);
    }
}
