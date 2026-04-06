using System;
using System.Collections.Generic;
using System.Linq;
using SonicEddy.Contracts.FilterGraph;
using SonicEddy.Controls.GraphEditorControl;
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
            viewModel.InPorts.OfType<PortViewModelBase>().Select(p =>
                    new FilterGraphOutputInputPort(p.Id, p.Name))
                .ToList();
        return new(id, ports);
    }

    public static FilterGraphInput ToGrpc(this InputNodeViewModel viewModel,
        Guid id)
    {
        var ports =
            viewModel.OutPorts.OfType<PortViewModelBase>().Select(p =>
                    new FilterGraphInputOutputPort(p.Id, p.Name))
                .ToList();
        return new(id, ports);
    }

    public static FilterGraph ToGrpc(this FilterGraphBuilderViewModel viewModel)
    {
        var nodes = viewModel.Nodes.Select(BaseToGrpc).ToList();
        nodes.Add(viewModel.InputNodeViewModel.BaseToGrpc());
        nodes.Add(viewModel.OutputNodeViewModel.BaseToGrpc());

        var edges = new List<FilterGraphEdge>();

        foreach (var edge in viewModel.Connections)
        {
            if (edge.Source is not PortViewModelBase source ||
                edge.Target is not PortViewModelBase target)
                throw new ArgumentException("Wrong port type");

            edges.Add(new(source.Id, target.Id));
        }

        return new(viewModel.Id, viewModel.Name, nodes, edges, null);
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
        BaseToGrpc(this GraphNode viewModel) =>
        viewModel switch
        {
            InputNodeViewModel n => n.ToGrpc(n.Id),
            OutputNodeViewModel n => n.ToGrpc(n.Id),
            Lv2PluginNodeViewModel n => n.ToGrpc(n.Id),
            _ => throw new NotImplementedException()
        };
}