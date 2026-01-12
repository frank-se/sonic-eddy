using System.Collections.Generic;
using ReactiveUI;

namespace SonicEddy.ViewModels.FilterGraphBuilderViewModels;

public abstract class NodeBase
    : ReactiveObject
{
    private string _name = string.Empty;

    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    private double _x;

    public double X
    {
        get => _x;
        set => this.RaiseAndSetIfChanged(ref _x, value);
    }

    private double _y;

    public double Y
    {
        get => _y;
        set => this.RaiseAndSetIfChanged(ref _y, value);
    }

    public List<PortNodeBase> InPorts { get; init; } = [];
    public List<PortNodeBase> OutPorts { get; init; } = [];
}