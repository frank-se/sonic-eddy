using System;
using ReactiveUI;

namespace SonicEddy.ViewModels.FilterGraphBuilderViewModels;

public class ParameterItemViewModel : ReactiveObject
{
    private string _displayName;
    private double _default;

    public ParameterItemViewModel(Guid nodeId, string nodeName, string symbol,
        string displayName, double min, double max, double @default)
    {
        NodeId = nodeId;
        NodeName = nodeName;
        Symbol = symbol;
        Min = min;
        Max = max;
        _displayName = displayName;
        _default = @default;
    }

    public Guid NodeId { get; }
    public string NodeName { get; }
    public string Symbol { get; }
    public double Min { get; }
    public double Max { get; }
    public string MinLabel => $"Min: {Min:F2}";
    public string MaxLabel => $"Max: {Max:F2}";

    public string DisplayName
    {
        get => _displayName;
        set => this.RaiseAndSetIfChanged(ref _displayName, value);
    }

    public double Default
    {
        get => _default;
        set => this.RaiseAndSetIfChanged(ref _default, value);
    }
}
