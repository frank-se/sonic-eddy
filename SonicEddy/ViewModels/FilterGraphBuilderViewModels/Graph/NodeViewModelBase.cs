using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ReactiveUI;
using SonicEddy.Controls.GraphEditorControl;

namespace SonicEddy.ViewModels.FilterGraphBuilderViewModels.Graph;

public abstract class NodeViewModelBase(
    string name,
    ReadOnlyCollection<PortViewModelBase> inPorts,
    ReadOnlyCollection<PortViewModelBase> outPorts) : GraphNode(name,
    new(inPorts.OfType<GraphPort>().ToList()),
    new(outPorts.OfType<GraphPort>().ToList()))
{
    public Guid Id
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = Guid.NewGuid();

    private string _name = string.Empty;
}