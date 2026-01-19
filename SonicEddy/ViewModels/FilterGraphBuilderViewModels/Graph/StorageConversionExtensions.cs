using System;
using System.Collections.Generic;
using System.Linq;
using SonicEddy.Contracts.FilterGraph;
using SonicEddy.ViewModels.FilterGraphBuilderViewModels.Graph.Graph;
using SonicEddy.ViewModels.FilterGraphBuilderViewModels.Graph.Lv2;

namespace SonicEddy.ViewModels.FilterGraphBuilderViewModels.Graph;

public static class StorageConversionExtensions
{
    public static FilterGraphLv2Plugin
        ToGrpc(
            this Lv2PluginNodeViewModel viewModel, Guid id)
    {
        var inputPorts =
            viewModel.InPorts.OfType<Lv2PortViewModel>().Select(p =>
                new FilterGraphLv2InputPort(p.Id, p.Name,
                    p.Description.Symbol)).ToList();

        var outputPorts =
            viewModel.OutPorts.OfType<Lv2PortViewModel>().Select(p =>
                new FilterGraphLv2OutputPort(p.Id, p.Name,
                    p.Description.Symbol)).ToList();

        return new(id, viewModel.Name, viewModel.Plugin.Uri, inputPorts,
            outputPorts);
    }

    public static FilterGraphOutput ToGrpc(this OutputNodeViewModel viewModel,
        Guid id)
    {
        var ports =
            viewModel.InPorts.Select(p =>
                    new FilterGraphOutputInputPort(p.Id, p.Name))
                .ToList();
        return new(id, ports);
    }

    public static FilterGraphInput ToGrpc(this InputNodeViewModel viewModel,
        Guid id)
    {
        var ports =
            viewModel.OutPorts.Select(p =>
                    new FilterGraphInputOutputPort(p.Id, p.Name))
                .ToList();
        return new(id, ports);
    }

    public static FilterGraph ToGrpc(this FilterGraphBuilderViewModel viewModel)
    {
        var nodes = viewModel.Nodes.Select(BaseToGrpc).ToList();

        var edges = new List<FilterGraphEdge>();

        foreach (var (sourcePort, targetPort) in viewModel.Connections)
        {
            var sourceNodeIndex =
                viewModel.Nodes.IndexOf(sourcePort.NodeViewModel);

            var sourcePortIndex =
                sourcePort.NodeViewModel.OutPorts.IndexOf(sourcePort);

            var sourcePortId =
                IdOutputPortByIndex(nodes[sourceNodeIndex], sourcePortIndex);

            var targetNodeIndex =
                viewModel.Nodes.IndexOf(targetPort.NodeViewModel);

            var targetPortIndex =
                targetPort.NodeViewModel.InPorts.IndexOf(targetPort);

            var targetPortId =
                IdInputPortByIndex(nodes[targetNodeIndex], targetPortIndex);

            edges.Add(new(sourcePortId, targetPortId));
        }

        return new(viewModel.Id, viewModel.Name, nodes, edges);
    }

    private static Guid
        IdInputPortByIndex(FilterGraphNodeBase node, int index) =>
        node switch
        {
            FilterGraphOutput n => n.InputPorts[index].Id,
            FilterGraphLv2Plugin n => n.InputPorts[index].Id,
            _ => throw new NotImplementedException()
        };

    private static Guid
        IdOutputPortByIndex(FilterGraphNodeBase node, int index) =>
        node switch
        {
            FilterGraphInput n => n.OutputPorts[index].Id,
            FilterGraphLv2Plugin n => n.OutputPorts[index].Id,
            _ => throw new NotImplementedException()
        };

    private static FilterGraphNodeBase
        BaseToGrpc(this NodeViewModelBase viewModel) =>
        viewModel switch
        {
            InputNodeViewModel n => n.ToGrpc(n.Id),
            OutputNodeViewModel n => n.ToGrpc(n.Id),
            Lv2PluginNodeViewModel n => n.ToGrpc(n.Id),
            _ => throw new NotImplementedException()
        };
}