using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.Primitives;
using DynamicData;
using Fr.Wireplumber.Model.Config.FilterChain;
using ReactiveUI;
using SonicEddy.Services.AppData;

namespace SonicEddy.ViewModels.FilterGraphBuilderViewModels;

public class FilterGraphBuilderViewModel : ViewModelBase, IActivatableViewModel,
    IRoutableViewModel
{
    public FilterGraphBuilderViewModel(IAppDataService appDataService,
        string? urlPathSegment, IScreen hostScreen)
    {
        _appDataService = appDataService;
        UrlPathSegment = urlPathSegment;
        HostScreen = hostScreen;

        Nodes = [_filterChainInputsNode, _filterChainOutputsNode];

        _ = Task.Run(BuildPluginClasses);
    }

    public ObservableCollection<PortConnection> Connections { get; } = [];

    private readonly FilterChainInputsNode _filterChainInputsNode =
        new FilterChainInputsNode()
        {
            Name = "Input Ports",
            X = 0, Y = 0
        };

    private readonly FilterChainOutputsNode _filterChainOutputsNode =
        new FilterChainOutputsNode()
        {
            Name = "Output Ports",
            X = 400, Y = 0
        };

    public ObservableCollection<NodeBase> Nodes { get; }

    public ObservableCollection<Lv2PluginClass>
        AvailablePluginsByClass { get; } = [];

    private double _currentX = 100;
    private double _currentY = 10;

    public void AddPlugin(Lv2Plugin plugin)
    {
        Nodes.Add(new Lv2PluginNode(plugin.Description)
        {
            X = _currentX, Y = _currentY, Name = plugin.Description.Name
        });

        _currentX += 200;
    }

    public void Connect(PortNodeBase outPort, PortNodeBase inPort) =>
        Connections.Add(new(outPort, inPort));

    private void BuildPluginClasses()
    {
        var classDescriptions = Fr.Lv2.Lv2.ClassDescriptions();
        var classes = classDescriptions.ToDictionary(x => x.Uri,
            x => new Lv2PluginClass(x.Uri, x.Label, []));

        var pluginDescriptions = Fr.Lv2.Lv2.PluginDescriptions();
        foreach (var pluginDescription in pluginDescriptions)
        {
            classes[pluginDescription.ClassUri].Plugins
                .Add(new Lv2Plugin(pluginDescription));
        }

        var classesWithDescriptions =
            classes.Values.Where(c => c.Plugins.Any());

        AvailablePluginsByClass.AddRange(classesWithDescriptions);
    }

    public string? UrlPathSegment { get; }
    public IScreen HostScreen { get; }
    public ViewModelActivator Activator { get; } = new();

    private readonly IAppDataService _appDataService;
}