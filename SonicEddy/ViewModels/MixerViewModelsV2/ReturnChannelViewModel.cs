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
        IMixerService mixerService)
    {
        Text = text;
        SelectChannelCommand = selectChannelCommand;
        _inputLoopback = inputLoopback;
        _outbackLoopback = outbackLoopback;
        _appDataService = appDataService;
        _mixerService = mixerService;
        FilterGraph = filterGraph;

        this.WhenAnyValue(x => x.FilterChain)
            .Subscribe(chain =>
            {
                if (chain is null)
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

                Parameters = [];

                HasFilter = true;
            })
            .DisposeWith(_disposable!);

        PanAndVolume = new PanAndVolumeViewModel(outbackLoopback.PlaybackNode);

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

    public async Task ApplyFilterConfigurationAsync(int layerId, int index,
        FilterConfig? config)
    {
        if (config is null)
        {
            if (FilterChain is null) return;

            await _mixerService.RemoveFilterFromReturnChannel(layerId, index);
            FilterChain = null;
            FilterGraph = null;
            return;
        }

        var graph = await _appDataService.GetFilterGraph(config.FilterGraphId);
        if (FilterGraph?.Id != graph.Id || FilterChain is null)
        {
            FilterChain = await _mixerService.AddFilterToReturnChannel(
                layerId, index, graph);
            FilterGraph = graph;
        }

        var node = FilterChain?.CaptureNode;
        if (node is null) return;
        foreach (var value in config.Values)
            node.SetParam(value.FullyQualifiedName, value.Value);
    }

    public FilterConfig? CaptureFilterConfiguration() =>
        FilterGraph is null
            ? null
            : new()
            {
                FilterGraphId = FilterGraph.Id,
                Values = CaptureFilterValues()
            };

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

    public void OnAddFilter(IOutputChannel channel)
    {
        throw new System.NotImplementedException();
    }

    public void OnDeleteFilter(IOutputChannel channel)
    {
        throw new System.NotImplementedException();
    }

    public void Dispose()
    {
        if (PanAndVolume is PanAndVolumeViewModel panAndVolume)
            panAndVolume.Dispose();

        _disposable?.Dispose();
        _disposable = null;

        GC.SuppressFinalize(this);
    }
}
