using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using DynamicData;
using ReactiveUI;
using SonicEddy.Services.AppData;

namespace SonicEddy.ViewModels.FilterGraphManagerViewModels;

public class FilterGraphManagerViewModel : ViewModelBase, IRoutableViewModel,
    IActivatableViewModel
{
    public FilterGraphManagerViewModel(IAppDataService appDataService,
        string? urlPathSegment, IScreen hostScreen)
    {
        _appDataService = appDataService;
        UrlPathSegment = urlPathSegment;
        HostScreen = hostScreen;

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

    private readonly IAppDataService _appDataService;

    public string? UrlPathSegment { get; }
    public IScreen HostScreen { get; }
    public ViewModelActivator Activator { get; } = new();
}