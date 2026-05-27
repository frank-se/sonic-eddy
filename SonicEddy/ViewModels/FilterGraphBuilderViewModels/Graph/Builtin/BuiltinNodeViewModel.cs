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
    public IReadOnlyList<(string Name, double Value)> Controls { get; } =
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

    private static IReadOnlyList<(string Name, double Value)> BuildControls(
        BuiltinNodeType type, int n)
    {
        if (type == BuiltinNodeType.Mixer)
            return Enumerable.Range(1, n)
                .Select(i => ($"Gain {i}", 1.0))
                .ToList();

        return BuiltinNodeCatalog.Get(type).DefaultControls
            .Select(c => (c.Name, c.Default))
            .ToList();
    }
}
