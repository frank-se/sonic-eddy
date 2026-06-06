using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using ReactiveUI;
using SonicEddy.Contracts.FilterGraph;
using SonicEddy.Contracts.ExternalEffects;
using SonicEddy.Services.AppData;
using SonicEddy.Services.ExternalEffects;

namespace SonicEddy.ViewModels.MixerViewModelsV2;

public class AddFilterChainViewModel : ViewModelBase, IDisposable
{
    private readonly CompositeDisposable _disposables = new();
    private readonly IAppDataService _appDataService;
    private readonly IExternalEffectService _externalEffects;

    public AddFilterChainViewModel(IAppDataService appDataService,
        IExternalEffectService externalEffects)
    {
        _appDataService = appDataService;
        _externalEffects = externalEffects;

        this.WhenAnyValue(x => x.SelectedFilterGraph)
            .Subscribe(graph =>
            {
                if (graph is not null) SelectedExternalEffect = null;
                UpdateIsButtonEnabled();
            })
            .DisposeWith(_disposables);
        this.WhenAnyValue(x => x.SelectedExternalEffect)
            .Subscribe(effect =>
            {
                if (effect is not null) SelectedFilterGraph = null;
                UpdateIsButtonEnabled();
            })
            .DisposeWith(_disposables);

        _ = GetFilterGraphs();
        RefreshExternalEffects();
        _externalEffects.Changed += RefreshExternalEffects;
    }

    private void UpdateIsButtonEnabled()
    {
        IsButtonEnabled = SelectedFilterGraph is not null ||
                          SelectedExternalEffect?.CanSelect == true;
    }

    public ObservableCollection<FilterGraph> FilterGraphs { get; } = [];
    public ObservableCollection<ExternalEffectChoiceViewModel>
        ExternalEffects { get; } = [];

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

    public ExternalEffectChoiceViewModel? SelectedExternalEffect
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    private void RefreshExternalEffects()
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            ExternalEffects.Clear();
            ExternalEffects.AddRange(_externalEffects.Effects.Select(effect =>
                new ExternalEffectChoiceViewModel(effect,
                    _externalEffects.IsAvailable(effect.Id),
                    _externalEffects.GetUsedBy(effect.Id))));
        });
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

    public bool DialogResult;

    public bool IsButtonEnabled
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public void Dispose()
    {
        _externalEffects.Changed -= RefreshExternalEffects;
        _disposables.Dispose();
    }
}

public sealed class ExternalEffectChoiceViewModel(
    ExternalEffectConfig config,
    bool available,
    string? usedBy)
{
    public ExternalEffectConfig Config { get; } = config;
    public string Name => Config.Name;
    public string Status => usedBy is not null
        ? $"Used by {usedBy}"
        : available ? "Available" : "Unavailable";
    public bool CanSelect => usedBy is null;
}
