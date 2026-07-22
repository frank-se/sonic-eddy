using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using Fr.Sonic.Model.Lv2;
using ReactiveUI;
using SonicEddy.Contracts.FilterGraph;
using SonicEddy.Controls.GraphEditorControl;
using SonicEddy.Services.AppData;
using SonicEddy.Tools;
using SonicEddy.ViewModels.FilterGraphBuilderViewModels.Graph;
using SonicEddy.ViewModels.FilterGraphBuilderViewModels.Graph.Builtin;
using SonicEddy.ViewModels.FilterGraphBuilderViewModels.Graph.Graph;
using SonicEddy.ViewModels.FilterGraphBuilderViewModels.Graph.Lv2;
using SonicEddy.ViewModels.FilterGraphBuilderViewModels.Toolbox;
using SonicEddy.ViewModels.SaveFilterGraphDialogViewModels;
using SonicEddy.Views.SaveFilterGraphDialogViews;

namespace SonicEddy.ViewModels.FilterGraphBuilderViewModels;

public class FilterGraphBuilderViewModel : ViewModelBase
{
    public FilterGraphBuilderViewModel(IAppDataService appDataService,
        List<ClassDescription> pluginClasses,
        List<PluginDescription> pluginDescriptions)
    {
        _appDataService = appDataService;

        SelectNodeCommand = ReactiveCommand.Create<GraphNode?>(node =>
        {
            _selectedNode = node;
            this.RaisePropertyChanged(nameof(SelectedNode));
            this.RaisePropertyChanged(nameof(SelectedNodeAsLv2));
            this.RaisePropertyChanged(nameof(SelectedNodeAsBuiltin));
            this.RaisePropertyChanged(nameof(SelectedNodeParameterGroup));
        });

        MovePluginUpCommand = ReactiveCommand.Create<ParameterPluginViewModel>(g =>
        {
            var i = ParameterPlugins.IndexOf(g);
            if (i > 0) ParameterPlugins.Move(i, i - 1);
        });

        MovePluginDownCommand = ReactiveCommand.Create<ParameterPluginViewModel>(g =>
        {
            var i = ParameterPlugins.IndexOf(g);
            if (i < ParameterPlugins.Count - 1) ParameterPlugins.Move(i, i + 1);
        });

        MoveParameterUpCommand = ReactiveCommand.Create<ParameterItemViewModel>(p =>
        {
            var group = ParameterPlugins.FirstOrDefault(g => g.NodeId == p.NodeId);
            if (group == null) return;
            var i = group.Parameters.IndexOf(p);
            if (i > 0) group.Parameters.Move(i, i - 1);
        });

        MoveParameterDownCommand = ReactiveCommand.Create<ParameterItemViewModel>(p =>
        {
            var group = ParameterPlugins.FirstOrDefault(g => g.NodeId == p.NodeId);
            if (group == null) return;
            var i = group.Parameters.IndexOf(p);
            if (i < group.Parameters.Count - 1) group.Parameters.Move(i, i + 1);
        });

        _ = Task.Run(() => BuildPluginClasses(pluginClasses, pluginDescriptions));
    }

    public string Name
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public Guid Id
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = Guid.NewGuid();

    public ObservableCollection<GraphEdge> Connections { get; } = [];
    public ObservableCollection<GraphNode> Nodes { get; } = [];
    public ObservableCollection<ParameterPluginViewModel> ParameterPlugins { get; } = [];

    public InputNodeViewModel InputNodeViewModel
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = new() { Id = Guid.NewGuid() };

    public OutputNodeViewModel OutputNodeViewModel
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = new() { Id = Guid.NewGuid() };

    public ObservableCollection<Lv2PluginClass> AvailablePluginsByClass { get; } = [];

    public ObservableCollection<BuiltinNodeGroupViewModel> AvailableBuiltins { get; } =
        new(BuiltinNodeCatalog.Groups.Select(g => new BuiltinNodeGroupViewModel(g.Name, g.Types)));

    private GraphNode? _selectedNode;
    public GraphNode? SelectedNode => _selectedNode;
    public Lv2PluginNodeViewModel? SelectedNodeAsLv2 => _selectedNode as Lv2PluginNodeViewModel;
    public BuiltinNodeViewModel? SelectedNodeAsBuiltin => _selectedNode as BuiltinNodeViewModel;
    public ParameterPluginViewModel? SelectedNodeParameterGroup =>
        _selectedNode is Lv2PluginNodeViewModel lv2
            ? ParameterPlugins.FirstOrDefault(g => g.NodeId == lv2.Id)
            : null;

    public ReactiveCommand<GraphNode?, Unit> SelectNodeCommand { get; }
    public ReactiveCommand<ParameterPluginViewModel, Unit> MovePluginUpCommand { get; }
    public ReactiveCommand<ParameterPluginViewModel, Unit> MovePluginDownCommand { get; }
    public ReactiveCommand<ParameterItemViewModel, Unit> MoveParameterUpCommand { get; }
    public ReactiveCommand<ParameterItemViewModel, Unit> MoveParameterDownCommand { get; }

    public void AddPlugin(object plugin)
    {
        var nodeVm = new Lv2PluginNodeViewModel(((AvailableLv2Plugin)plugin).Description);
        Nodes.Add(nodeVm);

        var group = new ParameterPluginViewModel(nodeVm.Id, nodeVm.Name);
        foreach (var ctrl in nodeVm.Controls)
        {
            var param = new ParameterItemViewModel(
                nodeVm.Id, nodeVm.Name, ctrl.Symbol, ctrl.DisplayName,
                ctrl.Min, ctrl.Max, ctrl.Value);
            param.WhenAnyValue(p => p.Default).Subscribe(v => ctrl.Value = v);
            group.Parameters.Add(param);
        }
        ParameterPlugins.Add(group);
    }

    public void AddBuiltinNode(object available)
    {
        var builtinNode = (AvailableBuiltinNode)available;
        Nodes.Add(new BuiltinNodeViewModel(builtinNode.NodeType, (int)builtinNode.ChannelCount));
    }

    private async Task BuildPluginClasses(
        List<ClassDescription> classDescriptions,
        List<PluginDescription> pluginDescriptions)
    {
        var classes = classDescriptions.ToDictionary(x => x.Uri,
            x => new Lv2PluginClass(x.Uri, x.Label, []));

        foreach (var pluginDescription in pluginDescriptions)
            classes[pluginDescription.ClassUri].Plugins.Add(new(pluginDescription));

        var classesWithDescriptions = classes.Values.Where(c => c.Plugins.Any());
        AvailablePluginsByClass.AddRange(classesWithDescriptions);
    }

    public async Task SaveGraph()
    {
        var dialogViewModel = new SaveFilterGraphDialogViewModel();
        var dialog = new SaveFilterGraphDialogView { DataContext = dialogViewModel };

        await dialog.ShowDialog(WindowTools.GetMainWindow()!);

        if (dialogViewModel.DialogResult)
        {
            Name = dialogViewModel.Name;
            try
            {
                var filterGraph = this.ToGrpc();
                await _appDataService.CreateFilterGraph(filterGraph);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Couldn't save filter graph {e.Message}");
            }
        }
    }

    private readonly IAppDataService _appDataService;
}
