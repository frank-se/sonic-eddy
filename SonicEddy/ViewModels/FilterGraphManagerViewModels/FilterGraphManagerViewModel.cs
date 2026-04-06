using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using DynamicData;
using SonicEddy.Services.AppData;
using SonicEddy.ViewModels.FilterGraphBuilderViewModels;
using SonicEddy.Views.FilterGraphBuilderViews;
using SonicEddy.Views.FilterGraphManagerViews;
using Splat;

namespace SonicEddy.ViewModels.FilterGraphManagerViewModels;

public class FilterGraphManagerViewModel : ViewModelBase
{
    private Window? _filterGraphBuilder;
    private Window? _filterGraphParameterEditor;

    public FilterGraphManagerViewModel(IAppDataService appDataService)
    {
        _appDataService = appDataService;

        _ = Task.Run(LoadFilterGraphs);
    }

    public ObservableCollection<FilterGraphViewModel>
        FilterGraphs { get; } = [];

    private async Task LoadFilterGraphs()
    {
        var filterGraphs =
            (await _appDataService.GetAllFilterGraphs()).Select(f =>
                new FilterGraphViewModel()
                {
                    Id = f.Id,
                    Name = f.Name,
                    NumberOfEdges = f.Edges.Count,
                    NumberOfNodes = f.Nodes.Count
                });

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            FilterGraphs.Clear();
            FilterGraphs.AddRange(filterGraphs);
        });
    }

    public void DeleteFilterGraph(Guid id)
    {
        _appDataService.DeleteFilterGraph(id);
        _ = Task.Run(LoadFilterGraphs);
    }

    public async Task EditFilterGraphParameterOrder(Guid id)
    {
        if (_filterGraphParameterEditor is not null &&
            _filterGraphParameterEditor.IsVisible) return;

        var filterGraph = await _appDataService.GetFilterGraph(id);

        var viewModel = new FilterGraphParameterEditorViewModel(filterGraph,
            Fr.Lv2.Lv2.PluginDescriptions(), _appDataService);

        _filterGraphParameterEditor = new FilterGraphParameterEditorWindow()
        {
            DataContext = viewModel
        };

        _filterGraphParameterEditor.Show();
    }

    public void ShowFilterGraphBuilderWindow()
    {
        if (_filterGraphBuilder is not null &&
            _filterGraphBuilder.IsVisible) return;

        var appDataService = Locator.Current.GetService<IAppDataService>();

        var viewModel = new FilterGraphBuilderViewModel(appDataService!,
            Fr.Lv2.Lv2.ClassDescriptions(),
            Fr.Lv2.Lv2.PluginDescriptions());

        _filterGraphBuilder = new FilterGraphBuilderWindow()
        {
            DataContext = viewModel
        };

        _filterGraphBuilder.Show();
    }

    private readonly IAppDataService _appDataService;
}