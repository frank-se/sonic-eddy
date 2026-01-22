using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using ReactiveUI;
using SonicEddy.Contracts.FilterGraph;
using SonicEddy.Services.AppData;

namespace SonicEddy.ViewModels.MixerViewModels;

public class AddFilterChainViewModel : ViewModelBase, IDisposable
{
    private readonly CompositeDisposable _disposables = new();
    private readonly IAppDataService _appDataService;

    public AddFilterChainViewModel(IAppDataService appDataService)
    {
        _appDataService = appDataService;

        this.WhenAnyValue(x => x.SelectedFilterGraph)
            .Subscribe(_ => UpdateIsButtonEnabled())
            .DisposeWith(_disposables);

        _ = GetFilterGraphs();
    }

    private void UpdateIsButtonEnabled()
    {
        IsButtonEnabled = SelectedFilterGraph is not null;
    }

    public ObservableCollection<FilterGraph> FilterGraphs { get; } = [];

    private async Task GetFilterGraphs()
    {
        var filterGraphs = await _appDataService.GetAllFilterGraphs();
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            FilterGraphs.Clear();
            FilterGraphs.AddRange(filterGraphs);
        });
    }

    public FilterGraph? SelectedFilterGraph
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public Interaction<Unit, Unit> Close { get; } = new();

    public async Task CancelAction()
    {
        DialogResult = false;
        await Close.Handle(Unit.Default);
    }

    public async Task AddModuleAction()
    {
        DialogResult = true;
        await Close.Handle(Unit.Default);
    }

    public void Dispose() => _disposables.Dispose();

    public bool DialogResult;

    public bool IsButtonEnabled
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }
}