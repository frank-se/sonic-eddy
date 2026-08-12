using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Threading.Tasks;
using System.Windows.Input;
using Fr.Sonic.Modules.Models;
using Fr.Sonic.Model.Params;
using ReactiveUI;
using SonicEddy.Controls.MixerControls;
using SonicEddy.Contracts.FilterGraph;
using SonicEddy.Contracts.Mixers;
using SonicEddy.Services.AppData;
using SonicEddy.Services.MixerServiceV2;
using SonicEddy.Services.Monitoring;
using SonicEddy.Services.ExternalEffects;
using Splat;
using SonicEddy.Tools;
using SonicEddy.Views.MixerViewsV2;

namespace SonicEddy.ViewModels.MixerViewModelsV2;

public class ReturnChannelViewModel : ReactiveObject, IReturnChannel,
    IDisposable
{
    private CompositeDisposable? _disposable = new();

    private readonly LoopbackModule _inputLoopback;
    private readonly LoopbackModule _outbackLoopback;

    public LoopbackModule InputLoopback => _inputLoopback;
    public LoopbackModule OutputLoopback => _outbackLoopback;

    public ReturnChannelViewModel(string text, ICommand selectChannelCommand,
        LoopbackModule inputLoopback, LoopbackModule outbackLoopback,
        FilterChain? filterChain, FilterGraph? filterGraph,
        IMonitoringService monitoringService, IAppDataService appDataService,
        IMixerService mixerService, int layerId, int index,
        bool isGlobal = false)
    {
        Text = text;
        SelectChannelCommand = selectChannelCommand;
        _inputLoopback = inputLoopback;
        _outbackLoopback = outbackLoopback;
        _appDataService = appDataService;
        _mixerService = mixerService;
        _layerId = layerId;
        _index = index;
        _isGlobal = isGlobal;
        _externalEffects =
            Locator.Current.GetService<IExternalEffectService>()!;
        FilterGraph = filterGraph;

        this.WhenAnyValue(x => x.FilterChain)
            .Subscribe(chain =>
            {
                if (chain is null || FilterGraph is null)
                {
                    HasFilter = false;
                    Parameters = [];
                    return;
                }

                // ReSharper disable once MergeIntoPattern
                // DO NOT CHANGE TO PATTERN
                // OTHERWISE ACCESS TO RESULT MIGHT BE BLOCKING!
                if (!chain.CaptureNode.Params.IsCompleted ||
                    chain.CaptureNode.Params.Result is null ||
                    !chain.CaptureNode.PropertyInfos.IsCompleted)
                    return;

                var propertyInfos =
                    chain.CaptureNode.PropertyInfos.Result?.PropertyInfos ?? [];

                Parameters = FilterGraph.FlatParameters?
                    .GroupBy(fp => fp.NodeId)
                    .Select(group =>
                    {
                        var node = FilterGraph.Nodes
                            .FirstOrDefault(n => n.Id == group.Key);
                        if (node is null) return null;

                        var parameters = group.Select((fp, i) =>
                            {
                                var fullyQualifiedName =
                                    $"{node.Name}:{fp.Symbol}";
                                var info = propertyInfos.FirstOrDefault(p =>
                                    p.Name == fullyQualifiedName);
                                var (min, max) =
                                    FilterChainParameterRange.Get(info);
                                return new ParameterViewModel(min, max,
                                    fp.DisplayName, i < 4, fullyQualifiedName,
                                    chain.CaptureNode);
                            })
                            .OfType<Controls.MixerControls.IParameter>()
                            .ToList();

                        return new ParameterCollection(node.Name, parameters);
                    })
                    .OfType<ParameterCollection>()
                    .ToList() ?? [];

                HasFilter = true;
            })
            .DisposeWith(_disposable!);

        PanAndVolume = new PanAndVolumeViewModelV2(outbackLoopback.PlaybackNode,
            monitoringService);

        FilterChain = filterChain;
    }

    public FilterChain? FilterChain
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public FilterGraph? FilterGraph
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    private readonly IAppDataService _appDataService;
    private readonly IMixerService _mixerService;
    private readonly IExternalEffectService _externalEffects;
    private readonly int _layerId;
    private readonly int _index;
    private readonly bool _isGlobal;
    public Guid? ExternalEffectId { get; private set; }

    public async Task ApplyFilterConfigurationAsync(int layerId, int index,
        InsertProcessorConfig? config)
    {
        if (config is null)
        {
            if (FilterChain is null && ExternalEffectId is null) return;

            if (_isGlobal)
                await _mixerService.RemoveFilterFromGlobalReturnChannel(index);
            else
                await _mixerService.RemoveFilterFromReturnChannel(layerId,
                    index);
            FilterChain = null;
            FilterGraph = null;
            ExternalEffectId = null;
            return;
        }

        if (config.Type == InsertProcessorType.ExternalEffect)
        {
            var processor = _isGlobal
                ? await _mixerService.AddExternalEffectToGlobalReturnChannel(
                    index, config.ProcessorId)
                : await _mixerService.AddExternalEffectToReturnChannel(
                    layerId, index, config.ProcessorId);
            FilterChain = null;
            FilterGraph = null;
            ExternalEffectId = processor?.ExternalEffectId;
            HasFilter = ExternalEffectId is not null;
            return;
        }

        var graph = await _appDataService.GetFilterGraph(config.ProcessorId);
        if (FilterGraph?.Id != graph.Id || FilterChain is null)
        {
            FilterGraph = graph;
            FilterChain = _isGlobal
                ? await _mixerService.AddFilterToGlobalReturnChannel(index,
                    graph)
                : await _mixerService.AddFilterToReturnChannel(layerId, index,
                    graph);
        }

        var node = FilterChain?.CaptureNode;
        if (node is null) return;
        foreach (var value in config.Values)
            node.SetParam(value.FullyQualifiedName, value.Value);
    }

    public InsertProcessorConfig? CaptureFilterConfiguration()
    {
        if (ExternalEffectId is { } externalEffectId)
            return new()
            {
                Type = InsertProcessorType.ExternalEffect,
                ProcessorId = externalEffectId
            };
        return FilterGraph is null
            ? null
            : new()
            {
                Type = InsertProcessorType.FilterChain,
                ProcessorId = FilterGraph.Id,
                Values = CaptureFilterValues()
            };
    }

    private List<FilterChainPresetValue> CaptureFilterValues()
    {
        var values = Parameters?.SelectMany(collection =>
                collection.Parameters)
            .Select(parameter => new FilterChainPresetValue(
                parameter.FullyQualifiedName, parameter.Value))
            .ToList() ?? [];
        if (values.Count > 0) return values;

        if (FilterChain?.CaptureNode.Params is not
            { IsCompletedSuccessfully: true } paramsTask ||
            paramsTask.Result is null)
            return [];

        return paramsTask.Result.Values
            .OfType<Parameter<float>>()
            .Select(parameter => new FilterChainPresetValue(
                parameter.Name, parameter.Value))
            .ToList();
    }

    public string Text { get; }

    public bool IsSelected
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public List<ParameterCollection>? Parameters
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool HasFilter
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public IPanAndVolume PanAndVolume { get; }

    public ICommand SelectChannelCommand { get; set; }

    public void OnSelectChannel() =>
        SelectChannelCommand.Execute(this);

    public async void OnAddFilter(object channel)
    {
        var viewModel = new AddFilterChainViewModel(_appDataService,
            _externalEffects);
        var dialog = new AddFilterChainView { DataContext = viewModel };
        await dialog.ShowDialog(WindowTools.GetMainWindow()!);
        if (!viewModel.DialogResult) return;

        if (viewModel.SelectedFilterGraph is { } graph)
        {
            ExternalEffectId = null;
            FilterGraph = graph;
            FilterChain = _isGlobal
                ? await _mixerService.AddFilterToGlobalReturnChannel(_index,
                    graph)
                : await _mixerService.AddFilterToReturnChannel(_layerId,
                    _index, graph);
        }
        else if (viewModel.SelectedExternalEffect is { } effect)
        {
            FilterChain = null;
            FilterGraph = null;
            var processor = _isGlobal
                ? await _mixerService.AddExternalEffectToGlobalReturnChannel(
                    _index, effect.Config.Id)
                : await _mixerService.AddExternalEffectToReturnChannel(
                    _layerId, _index, effect.Config.Id);
            ExternalEffectId = processor?.ExternalEffectId;
            HasFilter = ExternalEffectId is not null;
        }
    }

    public async void OnDeleteFilter(object channel)
    {
        if (_isGlobal)
            await _mixerService.RemoveFilterFromGlobalReturnChannel(_index);
        else
            await _mixerService.RemoveFilterFromReturnChannel(_layerId,
                _index);
        FilterChain = null;
        FilterGraph = null;
        ExternalEffectId = null;
        HasFilter = false;
    }

    public void Dispose()
    {
        if (PanAndVolume is IDisposable panAndVolume)
            panAndVolume.Dispose();

        _disposable?.Dispose();
        _disposable = null;

        GC.SuppressFinalize(this);
    }
}
