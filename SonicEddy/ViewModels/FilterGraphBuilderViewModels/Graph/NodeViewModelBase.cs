using System;
using System.Collections.Generic;
using ReactiveUI;

namespace SonicEddy.ViewModels.FilterGraphBuilderViewModels.Graph;

public abstract class NodeViewModelBase
    : ReactiveObject
{
    private Guid _id = Guid.NewGuid();

    public Guid Id
    {
        get => _id;
        set => this.RaiseAndSetIfChanged(ref _id, value);
    }

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

    public List<PortViewModelBase> InPorts { get; init; } = [];
    public List<PortViewModelBase> OutPorts { get; init; } = [];
}