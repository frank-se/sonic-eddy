using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using DynamicData;
using Fr.Sonic;
using SonicEddy.Services.AppData;
using SonicEddy.ViewModels.FilterGraphBuilderViewModels;
using SonicEddy.ViewModels.FilterGraphBuilderViewModels.Graph;
using SonicEddy.Views.FilterGraphBuilderViews;
using SonicEddy.Views.FilterGraphManagerViews;
using Splat;

namespace SonicEddy.ViewModels.FilterGraphManagerViewModels;

public class FilterGraphManagerViewModel : ViewModelBase
{
    private Window? _filterGraphBuilder;

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

    public void DeleteFilterGraph(object id)
    {
        _appDataService.DeleteFilterGraph((Guid)id);
        _ = Task.Run(LoadFilterGraphs);
    }

    public async Task EditFilterGraph(object id)
    {
        var filterGraph = await _appDataService.GetFilterGraph((Guid)id);
        var pluginDescriptions = FrSonicLv2.PluginDescriptions();
        var appDataService = Locator.Current.GetService<IAppDataService>();

        var viewModel = new FilterGraphBuilderViewModel(appDataService!,
            FrSonicLv2.ClassDescriptions(), pluginDescriptions);

        viewModel.LoadFromGrpc(filterGraph, pluginDescriptions);

        var window = new FilterGraphBuilderWindow { DataContext = viewModel };
        window.Show();
    }

    public void ShowFilterGraphBuilderWindow()
    {
        if (_filterGraphBuilder is not null &&
            _filterGraphBuilder.IsVisible) return;

        var appDataService = Locator.Current.GetService<IAppDataService>();

        var viewModel = new FilterGraphBuilderViewModel(appDataService!,
            FrSonicLv2.ClassDescriptions(),
            FrSonicLv2.PluginDescriptions());

        _filterGraphBuilder = new FilterGraphBuilderWindow()
        {
            DataContext = viewModel
        };

        _filterGraphBuilder.Show();
    }

    private readonly IAppDataService _appDataService;
}