using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fr.Lv2.Model;
using ReactiveUI;
using SonicEddy.Contracts.FilterGraph;
using SonicEddy.Services.AppData;

namespace SonicEddy.ViewModels.FilterGraphManagerViewModels;

public class FilterGraphParameterEditorViewModel : ViewModelBase
{
    private IAppDataService _appDataService;
    private FilterGraph _filterGraph;

    public FilterGraphParameterEditorViewModel(
        FilterGraph filterGraph,
        List<PluginDescription> pluginDescriptions,
        IAppDataService appDataService)
    {
        Plugins = BuildPlugins(filterGraph, pluginDescriptions);
        SelectedPlugin = Plugins.First();
        _appDataService = appDataService;
        _filterGraph = filterGraph;
    }

    public bool HasChanges
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = false;

    public async Task SaveChanges()
    {
        if (!HasChanges) return;

        var plugins =
            Plugins.Select(plugin =>
            {
                var parameters = plugin.Parameters
                    .OrderBy(p => p.SortOrder)
                    .Select(p =>
                        new FilterGraphParameterDescription(p.Name,
                            p.DisplayName,
                            p.Min, p.Max, p.Default)).ToList();
                return new FilterGraphPluginParameters(plugin.Node.Id,
                    parameters);
            });

        _filterGraph = _filterGraph with
        {
            PluginParameters = plugins.ToList()
        };

        await _appDataService.CreateFilterGraph(_filterGraph);

        HasChanges = false;
    }

    public FilterGraphPluginViewModel SelectedPlugin
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public List<FilterGraphPluginViewModel> Plugins { get; init; }

    private List<FilterGraphPluginViewModel> BuildPlugins(
        FilterGraph filterGraph, List<PluginDescription> pluginDescriptions)
    {
        return filterGraph.Nodes.OfType<FilterGraphLv2Plugin>()
            .Select(node =>
            {
                var existingPluginDesc =
                    filterGraph.PluginParameters?.FirstOrDefault(p =>
                        p.NodeId == node.Id);

                var parameters = existingPluginDesc?.ParameterDescriptions
                    .Select((
                            parameterDescription,
                            index) =>
                        new FilterGraphParameterDescriptionViewModel(
                            parameterDescription.Name,
                            parameterDescription.DisplayName,
                            parameterDescription.Min, parameterDescription.Max,
                            parameterDescription.Default, index * 10,
                            () => { HasChanges = true; }))
                    .ToList() ?? [];

                var visitedParameters =
                    parameters.Select(p => p.Name).ToHashSet();

                var lv2Spec =
                    pluginDescriptions.First(pD => pD.Uri == node.Uri);

                var controlPorts = lv2Spec.Ports.Where(p =>
                    p.Classes.Contains(Lv2PortUris.ControlPortUri));

                var index = (parameters.LastOrDefault()?.SortOrder ?? 0) + 10;

                foreach (var controlPort in controlPorts)
                {
                    var name = $"{node.Name}:{controlPort.Symbol}";
                    if (visitedParameters.Contains(name)) continue;

                    parameters.Add(new(name, controlPort.Name,
                        controlPort.Minimum ?? 0.0f,
                        controlPort.Maximum ?? 0.0f,
                        controlPort.Default ?? 0.0f, index,
                        () => { HasChanges = true; }));

                    index += 10;
                }

                return new FilterGraphPluginViewModel(parameters, node.Name,
                    node);
            }).ToList();
    }
}