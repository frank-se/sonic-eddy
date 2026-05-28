using System;
using System.Collections.Generic;
using System.Linq;
using Fr.Sonic.Model.Lv2;
using SonicEddy.Contracts.FilterGraph;
using SonicEddy.Controls.GraphEditorControl;
using SonicEddy.ViewModels.FilterGraphBuilderViewModels.Graph.Builtin;
using SonicEddy.ViewModels.FilterGraphBuilderViewModels.Graph.Lv2;

namespace SonicEddy.ViewModels.FilterGraphBuilderViewModels.Graph;

public static class StorageLoadExtensions
{
    public static void LoadFromGrpc(
        this FilterGraphBuilderViewModel vm,
        FilterGraph graph,
        List<PluginDescription> pluginDescriptions)
    {
        vm.Id = graph.Id;
        vm.Name = graph.Name;

        var portMap = new Dictionary<Guid, GraphPort>();

        var storedInput = graph.Nodes.OfType<FilterGraphInput>().First();
        vm.InputNodeViewModel.Id = storedInput.Id;
        RestorePortIds(vm.InputNodeViewModel.OutPorts,
            storedInput.OutputPorts.Select(p => (p.Id, p.Name)), portMap);

        var storedOutput = graph.Nodes.OfType<FilterGraphOutput>().First();
        vm.OutputNodeViewModel.Id = storedOutput.Id;
        RestorePortIds(vm.OutputNodeViewModel.InPorts,
            storedOutput.InputPorts.Select(p => (p.Id, p.Name)), portMap);

        foreach (var node in graph.Nodes)
        {
            switch (node)
            {
                case FilterGraphLv2Plugin lv2:
                {
                    var desc = pluginDescriptions.FirstOrDefault(p => p.Uri == lv2.Uri);
                    if (desc == null) continue;
                    var nodeVm = new Lv2PluginNodeViewModel(desc) { Id = lv2.Id };
                    RestorePortIds(nodeVm.InPorts,
                        lv2.InputPorts.Select(p => (p.Id, p.Name)), portMap);
                    RestorePortIds(nodeVm.OutPorts,
                        lv2.OutputPorts.Select(p => (p.Id, p.Name)), portMap);
                    foreach (var c in lv2.InitialControls)
                    {
                        var ctrl = nodeVm.Controls.FirstOrDefault(x => x.Symbol == c.Symbol);
                        if (ctrl != null) ctrl.Value = c.Value;
                    }
                    vm.Nodes.Add(nodeVm);
                    break;
                }
                case FilterGraphBuiltinNode builtin:
                {
                    var nodeVm = new BuiltinNodeViewModel(builtin.NodeType, builtin.ChannelCount)
                        { Id = builtin.Id };
                    RestorePortIds(nodeVm.InPorts,
                        builtin.InputPorts.Select(p => (p.Id, p.Name)), portMap);
                    RestorePortIds(nodeVm.OutPorts,
                        builtin.OutputPorts.Select(p => (p.Id, p.Name)), portMap);
                    foreach (var c in builtin.InitialControls)
                    {
                        var ctrl = nodeVm.Controls.FirstOrDefault(x => x.Name == c.Name);
                        if (ctrl != null) ctrl.Value = c.Value;
                    }
                    vm.Nodes.Add(nodeVm);
                    break;
                }
            }
        }

        foreach (var edge in graph.Edges)
        {
            if (!portMap.TryGetValue(edge.Source, out var source)) continue;
            if (!portMap.TryGetValue(edge.Target, out var target)) continue;
            vm.Connections.Add(new GraphEdge("edge", source, target));
        }
    }

    private static void RestorePortIds(
        IEnumerable<GraphPort> ports,
        IEnumerable<(Guid Id, string Name)> storedPorts,
        Dictionary<Guid, GraphPort> map)
    {
        var portList = ports.OfType<PortViewModelBase>().ToList();
        var storedList = storedPorts.ToList();
        for (var i = 0; i < Math.Min(portList.Count, storedList.Count); i++)
        {
            portList[i].Id = storedList[i].Id;
            map[storedList[i].Id] = portList[i];
        }
    }
}
