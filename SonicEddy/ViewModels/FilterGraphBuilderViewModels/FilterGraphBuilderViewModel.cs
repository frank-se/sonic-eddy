using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using DynamicData;
using Fr.Lv2.Model;
using ReactiveUI;
using SonicEddy.Controls.GraphEditorControl;
using SonicEddy.Services.AppData;
using SonicEddy.Tools;
using SonicEddy.ViewModels.FilterGraphBuilderViewModels.Graph;
using SonicEddy.ViewModels.FilterGraphBuilderViewModels.Graph.Graph;
using SonicEddy.ViewModels.FilterGraphBuilderViewModels.Graph.Lv2;
using SonicEddy.ViewModels.FilterGraphBuilderViewModels.Toolbox;
using SonicEddy.ViewModels.SaveFilterGraphDialogViewModels;
using SonicEddy.Views.SaveFilterGraphDialogViews;

namespace SonicEddy.ViewModels.FilterGraphBuilderViewModels;

public class FilterGraphBuilderViewModel : ViewModelBase, IActivatableViewModel
{
    public FilterGraphBuilderViewModel(IAppDataService appDataService,
        Task<List<ClassDescription>> pluginClasses,
        Task<List<PluginDescription>> pluginDescriptions)
    {
        _appDataService = appDataService;

        _ = Task.Run(() =>
            BuildPluginClasses(pluginClasses, pluginDescriptions));
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

    public InputNodeViewModel InputNodeViewModel
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = new()
    {
        Id = Guid.NewGuid(),
    };

    public OutputNodeViewModel OutputNodeViewModel
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = new()
    {
        Id = Guid.NewGuid(),
    };

    public ObservableCollection<GraphNode> Nodes { get; } = [];

    public ObservableCollection<Lv2PluginClass>
        AvailablePluginsByClass { get; } = [];

    public void AddPlugin(AvailableLv2Plugin plugin)
    {
        Nodes.Add(new Lv2PluginNodeViewModel(plugin.Description));
    }

    private async Task BuildPluginClasses(
        Task<List<ClassDescription>> classDescriptionTask,
        Task<List<PluginDescription>> pluginDescriptionTask)
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

    public string? UrlPathSegment { get; }
    public IScreen HostScreen { get; }
    public ViewModelActivator Activator { get; } = new();

    private readonly IAppDataService _appDataService;
}