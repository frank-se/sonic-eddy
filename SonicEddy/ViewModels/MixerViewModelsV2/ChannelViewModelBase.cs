using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Threading.Tasks;
using System.Windows.Input;
using DynamicData;
using Fr.Sonic.PInvoke;
using Fr.Sonic.Modules.Models;
using ReactiveUI;
using SonicEddy.Contracts.FilterGraph;
using SonicEddy.Contracts.Mixers;
using SonicEddy.Controls.MixerControls;
using SonicEddy.Services.AppData;
using SonicEddy.Services.ExternalEffects;
using SonicEddy.Services.Midi;
using SonicEddy.Services.MixerServiceV2;
using SonicEddy.Services.Monitoring;
using SonicEddy.Tools;
using SonicEddy.Views.MixerViewsV2;
using SonicEddy.Views.SavePresetDialogViews;
using SonicEddy.ViewModels.SavePresetDialogViewModels;
using Splat;
using ChannelStrip = SonicEddy.Services.MixerServiceV2.ChannelStrip;

namespace SonicEddy.ViewModels.MixerViewModelsV2;

public abstract class ChannelViewModelBase : ViewModelBase, IChannel,
    IDisposable
{
    protected readonly CompositeDisposable Disposables = new();

    protected ChannelViewModelBase(ulong channelId, string text,
        ICommand selectChannelCommand, TwoNodePipewireModule inputLoopback,
        TwoNodePipewireModule outputLoopback,
        FilterChain? filterChain,
        FilterGraph? filterGraph,
        ObservableCollection<IRoutingTarget> audioToRoutingTargets,
        IRoutingTarget? selectedAudioToRoutingTarget,
        IAppDataService appDataService,
        IMixerService mixerService,
        IMonitoringService monitoringService,
        bool enableMonitoring,
        int layerId,
        IMidiControllerSetupService midiControllerSetupService,
        ChannelType channelType)
    {
        _layerId = layerId;
        AudioToRoutingTargets = audioToRoutingTargets;
        SelectChannelCommand = selectChannelCommand;
        _inputLoopback = inputLoopback;
        _outputLoopback = outputLoopback;
        Text = text;
        ChannelId = channelId;
        SelectedAudioToRoutingTarget = selectedAudioToRoutingTarget;
        _appDataService = appDataService;
        _mixerService = mixerService;
        _midiSetupService = midiControllerSetupService;
        _channelType = channelType;

        _midiControllerChannelId = channelId + (ulong)_layerId *
            (channelType == ChannelType.Channel
                ? (ulong)mixerService.NumberOfChannelsPerLayer
                : (ulong)mixerService.NumberOfGroupChannelsPerLayer);

        FilterGraph = filterGraph;
        FilterChain = filterChain;
        ExternalEffectId = null;
        _externalEffectService =
            Locator.Current.GetService<IExternalEffectService>()!;

        if (enableMonitoring)
        {
            PanAndVolume =
                new PanAndVolumeViewModelV2(_outputLoopback.PlaybackNode,
                    monitoringService);
        }
        else
        {
            PanAndVolume =
                new PanAndVolumeViewModel(_outputLoopback.PlaybackNode);
        }

        this.WhenAnyValue(x => x.SelectedAudioToRoutingTarget)
            .Subscribe(ApplyAudioRoutingTarget)
            .DisposeWith(Disposables);

        this.WhenAnyValue(x => x.Parameters)
            .Subscribe(parameters =>
            {
                FirstPluginParameters?.Clear();
                SecondPluginParameters?.Clear();
                ThirdPluginParameters?.Clear();

                if (parameters is null) return;

                var index = 0;
                foreach (var parameterCollection in parameters.Take(3))
                {
                    switch (index)
                    {
                        case 0:
                            FirstPluginText = parameterCollection.Name;
                            FirstPluginParameters?.AddRange(parameterCollection
                                .Parameters);
                            break;
                        case 1:
                            SecondPluginText = parameterCollection.Name;
                            SecondPluginParameters?.AddRange(parameterCollection
                                .Parameters);
                            break;
                        case 2:
                            ThirdPluginText = parameterCollection.Name;
                            ThirdPluginParameters?.AddRange(parameterCollection
                                .Parameters);
                            break;
                    }

                    index++;
                }

                _midiSetupService.ClearFilterParameters(channelType, ChannelId);

                foreach (var (parameterCollection, i) in parameters
                             .Select((x, i) => (x, i)))
                {
                    foreach (var parameter in parameterCollection.Parameters)
                    {
                        _midiSetupService.AddFilterParameter(channelType,
                            _midiControllerChannelId, (ulong)i,
                            parameter.FullyQualifiedName,
                            parameter.Minimum, parameter.Maximum);
                    }
                }
            })
            .DisposeWith(Disposables);

        this.WhenAnyValue(x => x.FilterChain)
            .Subscribe(OnFilterChainUpdate)
            .DisposeWith(Disposables);

        var hasFilter = this.WhenAnyValue(x => x.HasFilter);
        SavePresetCommand = ReactiveCommand.CreateFromTask(SavePreset, hasFilter);
        LoadPresetCommand = ReactiveCommand.Create<FilterChainPresetViewModel>(LoadPreset);
    }

    public ObservableCollection<FilterChainPresetViewModel> AvailablePresets { get; } = [];
    public ICommand SavePresetCommand { get; }
    public ICommand LoadPresetCommand { get; }

    private async Task SavePreset()
    {
        var dialogVm = new SavePresetDialogViewModel();
        var dialog = new SavePresetDialogView { DataContext = dialogVm };
        await dialog.ShowDialog(WindowTools.GetMainWindow()!);

        if (!dialogVm.DialogResult) return;

        var values = Parameters?
            .SelectMany(c => c.Parameters)
            .Select(p => new FilterChainPresetValue(p.FullyQualifiedName, p.Value))
            .ToList() ?? [];

        var preset = new FilterChainPreset(Guid.NewGuid(), dialogVm.Name,
            FilterGraph!.Id, values);
        await _appDataService.CreateFilterChainPreset(preset);
        await LoadPresetsForCurrentFilter();
    }

    private void LoadPreset(FilterChainPresetViewModel preset)
    {
        if (Parameters == null) return;
        var valueMap = preset.Values.ToDictionary(v => v.FullyQualifiedName, v => v.Value);
        foreach (var collection in Parameters)
        foreach (var parameter in collection.Parameters)
            if (valueMap.TryGetValue(parameter.FullyQualifiedName, out var value))
                parameter.Value = value;
    }

    private async Task LoadPresetsForCurrentFilter()
    {
        if (FilterGraph == null)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => AvailablePresets.Clear());
            return;
        }
        var presets = await _appDataService.GetPresetsForFilterGraph(FilterGraph.Id);
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            AvailablePresets.Clear();
            AvailablePresets.AddRange(presets.Select(p => new FilterChainPresetViewModel(p)));
        });
    }

    private void OnFilterChainUpdate(FilterChain? chain)
    {
        if (chain is null || FilterGraph is null)
        {
            HasFilter = false;
            Parameters = null;
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
                        var fullyQualifiedName = $"{node.Name}:{fp.Symbol}";
                        var info = propertyInfos.FirstOrDefault(p =>
                            p.Name == fullyQualifiedName);
                        var (min, max) = FilterChainParameterRange.Get(info);
                        return new ParameterViewModel(min, max,
                            fp.DisplayName, i < 4, fullyQualifiedName,
                            chain.CaptureNode);
                    })
                    .OfType<IParameter>()
                    .ToList();

                return new ParameterCollection(node.Name, parameters);
            })
            .OfType<ParameterCollection>()
            .ToList() ?? [];

        _midiSetupService.SetChannelFilterNode(_channelType,
            _midiControllerChannelId,
            chain.CaptureNode.ObjectId);

        HasFilter = true;
        _ = LoadPresetsForCurrentFilter();
    }

    private readonly TwoNodePipewireModule _inputLoopback;
    private readonly TwoNodePipewireModule _outputLoopback;

    public TwoNodePipewireModule InputLoopback => _inputLoopback;
    public TwoNodePipewireModule OutputLoopback => _outputLoopback;
    public string InputNodeName => _inputLoopback.CaptureNode.Name ?? string.Empty;
    protected readonly IAppDataService _appDataService;
    protected readonly IMixerService _mixerService;
    private readonly IExternalEffectService _externalEffectService;
    private readonly IMidiControllerSetupService _midiSetupService;
    protected readonly int _layerId;
    private readonly ulong _midiControllerChannelId;

    public FilterChain? FilterChain
    {
        get;
        protected set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public FilterGraph? FilterGraph
    {
        get;
        protected set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public Guid? ExternalEffectId
    {
        get;
        protected set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public ulong ChannelId
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string Text
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

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

    public ICommand SelectChannelCommand { get; set; }

    public void OnSelectChannel() =>
        SelectChannelCommand.Execute(this);

    public bool HasFilter
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = false;

    public bool IsFilterMidiControlled
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = false;

    public virtual async Task AddFilterAction()
    {
        var dialogViewModel = new AddFilterChainViewModel(_appDataService,
            _externalEffectService);
        var dialog = new AddFilterChainView()
        {
            DataContext = dialogViewModel
        };

        await dialog.ShowDialog(WindowTools.GetMainWindow()!);

        if (!dialogViewModel.DialogResult) return;

        if (dialogViewModel.SelectedFilterGraph is { } graph)
        {
            ExternalEffectId = null;
            FilterGraph = graph;
            if (this is MasterChannelViewModel)
                FilterChain = await _mixerService.AddFilterToMasterChannel(
                    _layerId, graph);
            else if (_channelType == ChannelType.GroupChannel)
                FilterChain = await _mixerService.AddFilterToGroupChannel(
                    _layerId, ChannelId, graph);
            else
                FilterChain = (await _mixerService.AddFilterToChannelStrip(
                    _layerId, ChannelId, graph)).FilterChain;
        }
        else if (dialogViewModel.SelectedExternalEffect is { } effect)
        {
            FilterChain = null;
            FilterGraph = null;
            InsertProcessor? processor;
            if (this is MasterChannelViewModel)
                processor = await _mixerService.AddExternalEffectToMasterChannel(
                    _layerId, effect.Config.Id);
            else if (_channelType == ChannelType.GroupChannel)
                processor = await _mixerService.AddExternalEffectToGroupChannel(
                    _layerId, ChannelId, effect.Config.Id);
            else
                processor = await _mixerService.AddExternalEffectToChannelStrip(
                    _layerId, ChannelId, effect.Config.Id);
            ExternalEffectId = processor?.ExternalEffectId;
            HasFilter = ExternalEffectId is not null;
            Parameters = null;
        }
    }

    public void DeleteFilterAction() => _ = DeleteInsertProcessorAsync();

    private async Task DeleteInsertProcessorAsync()
    {
        if (this is MasterChannelViewModel)
            await _mixerService.RemoveFilterFromMasterChannel(_layerId);
        else if (this is MicChannelViewModel)
            await _mixerService.RemoveFilterFromMicChannel();
        else if (_channelType == ChannelType.GroupChannel)
            await _mixerService.RemoveFilterFromGroupChannel(_layerId,
                ChannelId);
        else
            await _mixerService.RemoveFilterFromChannelStrip(_layerId,
                ChannelId);
        FilterChain = null;
        FilterGraph = null;
        ExternalEffectId = null;
        HasFilter = false;
        Parameters = null;
    }

    public async Task ApplyFilterConfigurationAsync(InsertProcessorConfig? config)
    {
        if (config is null)
        {
            if (FilterChain is null && ExternalEffectId is null) return;

            if (this is MasterChannelViewModel)
                await _mixerService.RemoveFilterFromMasterChannel(_layerId);
            else if (this is MicChannelViewModel)
                await _mixerService.RemoveFilterFromMicChannel();
            else if (_channelType == ChannelType.GroupChannel)
                await _mixerService.RemoveFilterFromGroupChannel(
                    _layerId, ChannelId);
            else
                await _mixerService.RemoveFilterFromChannelStrip(
                    _layerId, ChannelId);

            FilterChain = null;
            FilterGraph = null;
            ExternalEffectId = null;
            return;
        }

        if (config.Type == InsertProcessorType.ExternalEffect)
        {
            InsertProcessor? processor;
            if (this is MasterChannelViewModel)
                processor = await _mixerService.AddExternalEffectToMasterChannel(
                    _layerId, config.ProcessorId);
            else if (this is MicChannelViewModel)
                processor = await _mixerService.AddExternalEffectToMicChannel(
                    config.ProcessorId);
            else if (_channelType == ChannelType.GroupChannel)
                processor = await _mixerService.AddExternalEffectToGroupChannel(
                    _layerId, ChannelId, config.ProcessorId);
            else
                processor = await _mixerService.AddExternalEffectToChannelStrip(
                    _layerId, ChannelId, config.ProcessorId);
            FilterChain = null;
            FilterGraph = null;
            ExternalEffectId = processor?.ExternalEffectId;
            HasFilter = ExternalEffectId is not null;
            Parameters = null;
            return;
        }

        var graph = await _appDataService.GetFilterGraph(config.ProcessorId);
        if (FilterGraph?.Id != graph.Id || FilterChain is null)
        {
            FilterGraph = graph;
            if (this is MasterChannelViewModel)
                FilterChain = await _mixerService.AddFilterToMasterChannel(
                    _layerId, graph);
            else if (this is MicChannelViewModel)
                FilterChain = await _mixerService.AddFilterToMicChannel(graph);
            else if (_channelType == ChannelType.GroupChannel)
                FilterChain = await _mixerService.AddFilterToGroupChannel(
                    _layerId, ChannelId, graph);
            else
                FilterChain = (await _mixerService.AddFilterToChannelStrip(
                    _layerId, ChannelId, graph)).FilterChain;
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
            .OfType<Fr.Sonic.Model.Params.Parameter<float>>()
            .Select(parameter => new FilterChainPresetValue(
                parameter.Name, parameter.Value))
            .ToList();
    }

    public ObservableCollection<IParameter>? FirstPluginParameters { get; } =
        [];

    public string FirstPluginText
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public ObservableCollection<IParameter>? SecondPluginParameters { get; } =
        [];

    public string SecondPluginText
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public ObservableCollection<IParameter>? ThirdPluginParameters { get; } =
        [];

    public string ThirdPluginText
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public IPanAndVolume PanAndVolume { get; }

    public ObservableCollection<IRoutingTarget>
        AudioToRoutingTargets { get; }

    public IRoutingTarget? SelectedAudioToRoutingTarget
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool FirstPluginSelectedForMidi
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool SecondPluginSelectedForMidi
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool ThirdPluginSelectedForMidi
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public void SetMidiControlledSectionId(ulong sectionId)
    {
        FirstPluginSelectedForMidi = sectionId == 0;
        SecondPluginSelectedForMidi = sectionId == 1;
        ThirdPluginSelectedForMidi = sectionId == 2;
    }

    protected virtual void Dispose(bool disposing)
    {
        Disposables.Dispose();
        if (PanAndVolume is PanAndVolumeViewModel panAndVolume)
            panAndVolume.Dispose();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void ApplyAudioRoutingTarget(IRoutingTarget? routingTarget)
    {
        if (routingTarget is null) return;
        switch (routingTarget.Channel)
        {
            case ChannelViewModelBase channel:
                _outputLoopback.PlaybackNode.OverrideTargetObject(
                    channel._inputLoopback.CaptureNode.ObjectSerial.ToString());
                break;
            case OutputChannelViewModel output:
                _outputLoopback.PlaybackNode.OverrideTargetObject(
                    output.CaptureNodeObjectSerial.ToString());
                break;
        }
    }

    private readonly ChannelType _channelType;
}
