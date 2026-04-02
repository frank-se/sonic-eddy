using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using DynamicData;
using SonicEddy.Services.AppData;
using SonicEddy.ViewModels.FilterGraphBuilderViewModels;
using SonicEddy.Views.FilterGraphBuilderViews;
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

    public void DeleteFilterGraph(Guid id)
    {
        _appDataService.DeleteFilterGraph(id);
        _ = Task.Run(LoadFilterGraphs);
    }

    public void EditFilterGraphParameterOrder(Guid id) {}
    
    public void ShowFilterGraphBuilderWindow()
    {
        if (_filterGraphBuilder is not null &&
            _filterGraphBuilder.IsVisible) return;

        var appDataService = Locator.Current.GetService<IAppDataService>();

        var viewModel = new FilterGraphBuilderViewModel(appDataService!,
            Task.Run(Fr.Lv2.Lv2.ClassDescriptions),
            Task.Run(Fr.Lv2.Lv2.PluginDescriptions));

        _filterGraphBuilder = new FilterGraphBuilderWindow()
        {
            DataContext = viewModel
        };

        _filterGraphBuilder.Show();
    }

    private readonly IAppDataService _appDataService;
}