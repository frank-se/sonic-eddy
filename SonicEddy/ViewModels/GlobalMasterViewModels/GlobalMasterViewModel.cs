using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Fr.Sonic.Model.Objects;
using Fr.Sonic.Model.Params;
using ReactiveUI;
using SonicEddy.Contracts.FilterGraph;
using SonicEddy.Contracts.Mixers;
using SonicEddy.Controls.MixerControls;
using SonicEddy.Services.AppData;
using SonicEddy.Services.ExternalEffects;
using SonicEddy.Services.MixerServiceV2;
using SonicEddy.Services.TraktorZ1;
using SonicEddy.Tools;
using SonicEddy.ViewModels.MixerViewModelsV2;
using SonicEddy.Views.MixerViewsV2;
using Splat;
using UiParameter = SonicEddy.Controls.MixerControls.IParameter;

namespace SonicEddy.ViewModels.GlobalMasterViewModels;

public class GlobalMasterViewModel : ReactiveObject, IDisposable
{
    private readonly Node _xfadeNode;
    private readonly Node? _cueNode;
    private bool _internal;
    private readonly IMixerService _mixerService;
    private readonly IAppDataService _appDataService;
    private readonly IExternalEffectService _externalEffects;
    private InsertProcessor? _insertProcessor;

    public GlobalMasterViewModel(GlobalMasterChannel globalMaster,
        MasterChannelViewModel layerA, MasterChannelViewModel layerB,
        CueChannel? cue,
        ObservableCollection<IRoutingTarget> audioToRoutingTargets)
    {
        LayerA = layerA;
        LayerB = layerB;
        HasCue = cue is not null;
        AudioToRoutingTargets = audioToRoutingTargets;
        _mixerService = Locator.Current.GetService<IMixerService>()!;
        _appDataService = Locator.Current.GetService<IAppDataService>()!;
        _externalEffects =
            Locator.Current.GetService<IExternalEffectService>()!;
        SetProcessorState(globalMaster.InsertProcessor);

        _internal = true;
        SelectedAudioToRoutingTarget = audioToRoutingTargets.FirstOrDefault(
            t => t.Channel is OutputChannelViewModel output &&
                 globalMaster.OutputTargetObject is not null &&
                 output.CaptureNodeObjectSerial ==
                 globalMaster.OutputTargetObject.ObjectSerial)
            ?? audioToRoutingTargets.FirstOrDefault();
        _internal = false;

        _xfadeNode = globalMaster.CrossFader.CaptureNode;
        _xfadeNode.ParamsChanged += OnXfadeParamsChanged;
        if (_xfadeNode.Params.IsCompleted)
            OnXfadeParamsChanged(_xfadeNode.Params.Result);

        if (cue is not null)
        {
            _cueNode = cue.CrossFader.CaptureNode;
            _cueNode.ParamsChanged += OnCueParamsChanged;
            CueChannel = new CueChannelViewModel(cue, audioToRoutingTargets);
            if (_cueNode.Params.IsCompleted)
                OnCueParamsChanged(_cueNode.Params.Result);
        }
    }

    private void OnXfadeParamsChanged(
        Dictionary<string, Fr.Sonic.Model.Params.IParameter>? parameters)
    {
        if (parameters is null) return;
        _internal = true;
        if (parameters.TryGetValue("xfade:xfade", out var xp) && xp is Parameter<float> xf)
            Xfade = xf.Value;
        if (parameters.TryGetValue("xfade:shape", out var sp) && sp is Parameter<float> sf)
            ShapeIndex = sf.Value > 0.5f ? 1 : 0;
        if (parameters.TryGetValue("xfade:mode", out var mp) && mp is Parameter<float> mf)
            ModeIndex = mf.Value > 0.5f ? 1 : 0;
        _internal = false;
    }

    private void OnCueParamsChanged(
        Dictionary<string, Fr.Sonic.Model.Params.IParameter>? parameters)
    {
        if (parameters is null) return;
        _internal = true;
        if (parameters.TryGetValue("xfade:xfade", out var xp) && xp is Parameter<float> xf)
            CueXfade = xf.Value;
        if (parameters.TryGetValue("xfade:shape", out var sp) && sp is Parameter<float> sf)
            CueShapeIndex = sf.Value > 0.5f ? 1 : 0;
        if (parameters.TryGetValue("xfade:mode", out var mp) && mp is Parameter<float> mf)
            CueModeIndex = mf.Value > 0.5f ? 1 : 0;
        _internal = false;
    }

    public MasterChannelViewModel LayerA { get; }
    public MasterChannelViewModel LayerB { get; }
    public CueChannelViewModel? CueChannel { get; }
    public bool HasCue { get; }
    public ObservableCollection<IRoutingTarget> AudioToRoutingTargets { get; }

    public IRoutingTarget? SelectedAudioToRoutingTarget
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            if (!_internal && value?.Channel is OutputChannelViewModel output)
                _mixerService.SetGlobalMasterOutputTarget(
                    output.CaptureNodeObjectSerial);
        }
    }

    public string XfadeNodeSerial => _xfadeNode.ObjectSerial.ToString();
    public string? CueNodeSerial => _cueNode?.ObjectSerial.ToString();

    public bool HasInsertProcessor
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string InsertProcessorName
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = "None";

    public Guid? ExternalEffectId { get; private set; }
    public Contracts.FilterGraph.FilterGraph? FilterGraph { get; private set; }
    public ObservableCollection<UiParameter> FirstInsertParameters { get; } = [];
    public ObservableCollection<UiParameter> SecondInsertParameters { get; } = [];
    public ObservableCollection<UiParameter> ThirdInsertParameters { get; } = [];
    public string FirstInsertText { get; private set; } = string.Empty;
    public string SecondInsertText { get; private set; } = string.Empty;
    public string ThirdInsertText { get; private set; } = string.Empty;

    public async Task AddInsertProcessor()
    {
        var viewModel = new AddFilterChainViewModel(_appDataService,
            _externalEffects);
        var dialog = new AddFilterChainView { DataContext = viewModel };
        await dialog.ShowDialog(WindowTools.GetMainWindow()!);
        if (!viewModel.DialogResult) return;
        InsertProcessor? processor = null;
        if (viewModel.SelectedFilterGraph is { } graph)
        {
            processor = await _mixerService.SetGlobalMasterInsertProcessor(
                new FilterChainInsertRequest(graph));
            FilterGraph = graph;
        }
        else if (viewModel.SelectedExternalEffect is { } effect)
        {
            processor = await _mixerService.SetGlobalMasterInsertProcessor(
                new ExternalEffectInsertRequest(effect.Config.Id));
        }
        SetProcessorState(processor);
    }

    public async Task DeleteInsertProcessor()
    {
        await _mixerService.SetGlobalMasterInsertProcessor(null);
        SetProcessorState(null);
    }

    public async Task ApplyInsertProcessorAsync(InsertProcessorConfig? config)
    {
        if (config is null)
        {
            await _mixerService.SetGlobalMasterInsertProcessor(null);
            SetProcessorState(null);
            return;
        }
        InsertProcessor? processor;
        if (config.Type == InsertProcessorType.ExternalEffect)
        {
            processor = await _mixerService.SetGlobalMasterInsertProcessor(
                new ExternalEffectInsertRequest(config.ProcessorId));
        }
        else
        {
            var graph = await _appDataService.GetFilterGraph(config.ProcessorId);
            processor = await _mixerService.SetGlobalMasterInsertProcessor(
                new FilterChainInsertRequest(graph));
            FilterGraph = graph;
            if (processor?.FilterChain?.CaptureNode is { } node)
                foreach (var value in config.Values)
                    node.SetParam(value.FullyQualifiedName, value.Value);
        }
        SetProcessorState(processor);
    }

    public InsertProcessorConfig? CaptureInsertProcessor() =>
        ExternalEffectId is { } externalEffectId
            ? new()
            {
                Type = InsertProcessorType.ExternalEffect,
                ProcessorId = externalEffectId
            }
            : FilterGraph is not null
                ? new()
                {
                    Type = InsertProcessorType.FilterChain,
                    ProcessorId = FilterGraph.Id,
                    Values = CaptureInsertValues()
                }
                : null;

    private List<FilterChainPresetValue> CaptureInsertValues()
    {
        if (_insertProcessor?.FilterChain?.CaptureNode.Params is not
            { IsCompletedSuccessfully: true } paramsTask ||
            paramsTask.Result is null)
            return [];
        return paramsTask.Result.Values
            .OfType<Parameter<float>>()
            .Select(parameter => new FilterChainPresetValue(parameter.Name,
                parameter.Value)).ToList();
    }

    private void SetProcessorState(InsertProcessor? processor)
    {
        _insertProcessor = processor;
        ExternalEffectId = processor?.ExternalEffectId;
        FilterGraph = processor?.FilterGraph;
        HasInsertProcessor = processor is not null;
        InsertProcessorName = processor switch
        {
            FilterChainInsertProcessor filter => filter.FilterGraph.Name,
            ExternalEffectInsertProcessor external =>
                _externalEffects.Effects.FirstOrDefault(effect =>
                    effect.Id == external.ExternalEffectId)?.Name ??
                "External Effect",
            _ => "None"
        };
        BuildInsertParameters(processor);
        this.RaisePropertyChanged(nameof(FirstInsertText));
        this.RaisePropertyChanged(nameof(SecondInsertText));
        this.RaisePropertyChanged(nameof(ThirdInsertText));
    }

    private void BuildInsertParameters(InsertProcessor? processor)
    {
        FirstInsertParameters.Clear();
        SecondInsertParameters.Clear();
        ThirdInsertParameters.Clear();
        FirstInsertText = string.Empty;
        SecondInsertText = string.Empty;
        ThirdInsertText = string.Empty;
        if (processor is not FilterChainInsertProcessor filter ||
            !filter.FilterChain.CaptureNode.Params.IsCompleted ||
            filter.FilterChain.CaptureNode.Params.Result is null ||
            !filter.FilterChain.CaptureNode.PropertyInfos.IsCompleted)
            return;

        var collections = filter.FilterGraph.PluginParameters?.Select(plugin =>
        {
            var graphNode = filter.FilterGraph.Nodes.First(node =>
                node.Id == plugin.NodeId);
            var parameters = plugin.ParameterDescriptions.Select((parameter,
                    index) => new ParameterViewModel(parameter.Min,
                    parameter.Max, parameter.DisplayName, index < 4,
                    parameter.Name, filter.FilterChain.CaptureNode))
                .OfType<UiParameter>().ToList();
            return new ParameterCollection(graphNode.Name, parameters);
        }).Take(3).ToArray() ?? [];

        if (collections.ElementAtOrDefault(0) is { } first)
        {
            FirstInsertText = first.Name;
            foreach (var parameter in first.Parameters)
                FirstInsertParameters.Add(parameter);
        }
        if (collections.ElementAtOrDefault(1) is { } second)
        {
            SecondInsertText = second.Name;
            foreach (var parameter in second.Parameters)
                SecondInsertParameters.Add(parameter);
        }
        if (collections.ElementAtOrDefault(2) is { } third)
        {
            ThirdInsertText = third.Name;
            foreach (var parameter in third.Parameters)
                ThirdInsertParameters.Add(parameter);
        }
    }

    // Main xfade
    private double _xfade;
    public double Xfade
    {
        get => _xfade;
        set
        {
            this.RaiseAndSetIfChanged(ref _xfade, value);
            if (!_internal) _xfadeNode.SetParam("xfade:xfade", (float)value);
        }
    }

    private int _shapeIndex;
    public int ShapeIndex
    {
        get => _shapeIndex;
        set
        {
            this.RaiseAndSetIfChanged(ref _shapeIndex, value);
            if (!_internal) _xfadeNode.SetParam("xfade:shape", (float)value);
        }
    }

    private int _modeIndex;
    public int ModeIndex
    {
        get => _modeIndex;
        set
        {
            this.RaiseAndSetIfChanged(ref _modeIndex, value);
            if (!_internal) _xfadeNode.SetParam("xfade:mode", (float)value);
        }
    }

    // Cue xfade
    private double _cueXfade;
    public double CueXfade
    {
        get => _cueXfade;
        set
        {
            this.RaiseAndSetIfChanged(ref _cueXfade, value);
            if (!_internal) _cueNode?.SetParam("xfade:xfade", (float)value);
        }
    }

    private int _cueShapeIndex;
    public int CueShapeIndex
    {
        get => _cueShapeIndex;
        set
        {
            this.RaiseAndSetIfChanged(ref _cueShapeIndex, value);
            if (!_internal) _cueNode?.SetParam("xfade:shape", (float)value);
        }
    }

    private int _cueModeIndex;
    public int CueModeIndex
    {
        get => _cueModeIndex;
        set
        {
            this.RaiseAndSetIfChanged(ref _cueModeIndex, value);
            if (!_internal) _cueNode?.SetParam("xfade:mode", (float)value);
        }
    }

    public void Dispose()
    {
        _xfadeNode.ParamsChanged -= OnXfadeParamsChanged;
        if (_cueNode is not null) _cueNode.ParamsChanged -= OnCueParamsChanged;
        CueChannel?.Dispose();
        GC.SuppressFinalize(this);
    }
}
