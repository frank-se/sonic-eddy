using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using SonicEddy.Contracts.FilterGraph;
using SonicEddy.ViewModels.FilterGraphBuilderViewModels.Graph.Graph;
using SonicEddy.ViewModels.FilterGraphBuilderViewModels.Toolbox;

namespace SonicEddy.ViewModels.FilterGraphBuilderViewModels.Graph.Builtin;

public class BuiltinNodeViewModel(BuiltinNodeType type, int channelCount = 2)
    : NodeViewModelBase(
        BuiltinNodeCatalog.Get(type).DisplayName,
        BuildInPorts(type, channelCount),
        BuildOutPorts(type, channelCount))
{
    public BuiltinNodeType NodeType { get; } = type;
    public int ChannelCount { get; } = channelCount;
    public ReadOnlyCollection<BuiltinControlViewModel> Controls { get; } =
        BuildControls(type, channelCount);

    private static ReadOnlyCollection<PortViewModelBase> BuildInPorts(
        BuiltinNodeType type, int n)
    {
        List<PortViewModelBase> ports = type switch
        {
            BuiltinNodeType.Mixer or BuiltinNodeType.Multiply or BuiltinNodeType.Max =>
                Enumerable.Range(1, n)
                    .Select(i => (PortViewModelBase)new InputPortViewModel($"In {i}"))
                    .ToList(),
            BuiltinNodeType.DcBlock =>
                Enumerable.Range(1, n)
                    .Select(i => (PortViewModelBase)new InputPortViewModel($"In {i}"))
                    .ToList(),
            BuiltinNodeType.Sine or BuiltinNodeType.Ramp =>
                [],
            _ =>
                [new InputPortViewModel("In")]
        };
        return new ReadOnlyCollection<PortViewModelBase>(ports);
    }

    private static ReadOnlyCollection<PortViewModelBase> BuildOutPorts(
        BuiltinNodeType type, int n)
    {
        List<PortViewModelBase> ports = type switch
        {
            BuiltinNodeType.DcBlock =>
                Enumerable.Range(1, n)
                    .Select(i => (PortViewModelBase)new OutputPortViewModel($"Out {i}"))
                    .ToList(),
            _ =>
                [new OutputPortViewModel("Out")]
        };
        return new ReadOnlyCollection<PortViewModelBase>(ports);
    }

    private static ReadOnlyCollection<BuiltinControlViewModel> BuildControls(
        BuiltinNodeType type, int n)
    {
        if (type == BuiltinNodeType.Mixer)
            return new(Enumerable.Range(1, n)
                .Select(i => new BuiltinControlViewModel($"Gain {i}", 1.0, 0.0, 10.0))
                .ToList());

        return new(BuiltinNodeCatalog.Get(type).DefaultControls
            .Select(c => new BuiltinControlViewModel(c.Name, c.Default, c.Min, c.Max))
            .ToList());
    }
}
