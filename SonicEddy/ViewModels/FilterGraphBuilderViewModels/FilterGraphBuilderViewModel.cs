using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using DynamicData;
using Fr.Lv2.Model;
using ReactiveUI;
using SonicEddy.Services.AppData;
using SonicEddy.Tools;
using SonicEddy.ViewModels.FilterGraphBuilderViewModels.Graph;
using SonicEddy.ViewModels.FilterGraphBuilderViewModels.Graph.Graph;
using SonicEddy.ViewModels.FilterGraphBuilderViewModels.Graph.Lv2;
using SonicEddy.ViewModels.FilterGraphBuilderViewModels.Toolbox;
using SonicEddy.ViewModels.SaveFilterGraphDialogViewModels;
using SonicEddy.Views.SaveFilterGraphDialogViews;

namespace SonicEddy.ViewModels.FilterGraphBuilderViewModels;

public class FilterGraphBuilderViewModel : ViewModelBase, IActivatableViewModel,
    IRoutableViewModel
{
    public FilterGraphBuilderViewModel(IAppDataService appDataService,
        string? urlPathSegment, IScreen hostScreen,
        Task<List<ClassDescription>> pluginClasses,
        Task<List<PluginDescription>> pluginDescriptions)
    {
        _appDataService = appDataService;
        UrlPathSegment = urlPathSegment;
        HostScreen = hostScreen;

        Nodes = [_inputNodeViewModel, _outputNodeViewModel];

        _ = Task.Run(() =>
            BuildPluginClasses(pluginClasses, pluginDescriptions));
    }

    private string _name = string.Empty;

    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    private Guid _id = Guid.NewGuid();

    public Guid Id
    {
        get => _id;
        set => this.RaiseAndSetIfChanged(ref _id, value);
    }

    public ObservableCollection<PortConnection> Connections { get; } = [];

    private readonly InputNodeViewModel _inputNodeViewModel =
        new InputNodeViewModel()
        {
            Id = Guid.NewGuid(),
            Name = "Input",
            X = 0, Y = 0
        };

    private readonly OutputNodeViewModel _outputNodeViewModel =
        new OutputNodeViewModel()
        {
            Id = Guid.NewGuid(),
            Name = "Output",
            X = 400, Y = 0
        };

    public ObservableCollection<NodeViewModelBase> Nodes { get; }

    public ObservableCollection<Lv2PluginClass>
        AvailablePluginsByClass { get; } = [];

    private double _currentX = 100;
    private double _currentY = 10;

    public void AddPlugin(AvailableLv2Plugin plugin)
    {
        Nodes.Add(new Lv2PluginNodeViewModel(plugin.Description)
        {
            X = _currentX, Y = _currentY, Name = plugin.Description.Name
        });

        _currentX += 200;
    }

    public void Connect(PortViewModelBase outPort, PortViewModelBase inPort) =>
        Connections.Add(new(outPort, inPort));

    private async Task BuildPluginClasses(
        Task<List<ClassDescription>> classDescriptionTask,
        Task<List<PluginDescription>> pluginDescriptionTask
    )
    {
        var classDescriptions = await classDescriptionTask;
        var classes = classDescriptions.ToDictionary(x => x.Uri,
            x => new Lv2PluginClass(x.Uri, x.Label, []));

        var pluginDescriptions = await pluginDescriptionTask;
        foreach (var pluginDescription in pluginDescriptions)
        {
            classes[pluginDescription.ClassUri].Plugins
                .Add(new AvailableLv2Plugin(pluginDescription));
        }

        var classesWithDescriptions =
            classes.Values.Where(c => c.Plugins.Any());

        AvailablePluginsByClass.AddRange(classesWithDescriptions);
    }

    public async Task SaveGraph()
    {
        var dialogViewModel = new SaveFilterGraphDialogViewModel();
        var dialog = new SaveFilterGraphDialogView()
        {
            DataContext = dialogViewModel
        };

        await dialog.ShowDialog(WindowTools.GetMainWindow()!);

        if (dialogViewModel.DialogResult)
        {
            Name = dialogViewModel.Name;
            var filterGraph = this.ToGrpc();
            await _appDataService.CreateFilterGraph(filterGraph);
        }
    }

    public string? UrlPathSegment { get; }
    public IScreen HostScreen { get; }
    public ViewModelActivator Activator { get; } = new();

    private readonly IAppDataService _appDataService;
}