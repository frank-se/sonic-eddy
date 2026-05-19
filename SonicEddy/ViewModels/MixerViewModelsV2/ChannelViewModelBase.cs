using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Threading.Tasks;
using System.Windows.Input;
using DynamicData;
using Fr.Sonic.PInvoke;
using Fr.Sonic.Modules.Models;
using ReactiveUI;
using SonicEddy.Contracts.FilterGraph;
using SonicEddy.Controls.MixerControls;
using SonicEddy.Services.AppData;
using SonicEddy.Services.Midi;
using SonicEddy.Services.MixerServiceV2;
using SonicEddy.Services.Monitoring;
using SonicEddy.Tools;
using SonicEddy.Views.MixerViewsV2;
using ChannelStrip = SonicEddy.Services.MixerServiceV2.ChannelStrip;

namespace SonicEddy.ViewModels.MixerViewModelsV2;

public abstract class ChannelViewModelBase : ViewModelBase, IChannel,
    IDisposable
{
    protected readonly CompositeDisposable Disposables = new();

    protected ChannelViewModelBase(ulong channelId, string text,
        ICommand selectChannelCommand, LoopbackModule inputLoopback,
        LoopbackModule outputLoopback,
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
            .Subscribe(routingTarget =>
            {
                if (routingTarget is null) return;

                switch (routingTarget.Channel)
                {
                    case ChannelViewModelBase channel:
                        _outputLoopback.PlaybackNode.OverrideTargetObject(
                            channel._inputLoopback.CaptureNode.ObjectSerial
                                .ToString());
                        break;
                    case OutputChannelViewModel output:
                        _outputLoopback.PlaybackNode.OverrideTargetObject(
                            output.CaptureNodeObjectSerial.ToString());
                        break;
                }
            })
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

        Parameters = FilterGraph.PluginParameters?.Select(plugin =>
        {
            var node =
                FilterGraph.Nodes.First(n => n.Id == plugin.NodeId);

            var parameters =
                plugin.ParameterDescriptions.Select((p, i) =>
                        new ParameterViewModel(
                            p.Min, p.Max, p.DisplayName, i < 4, p.Name,
                            chain.CaptureNode))
                    .OfType<IParameter>()
                    .ToList();

            return new ParameterCollection(node.Name, parameters);
        }).ToList() ?? [];

        _midiSetupService.SetChannelFilterNode(_channelType,
            _midiControllerChannelId,
            chain.CaptureNode.ObjectId);

        HasFilter = true;
    }

    private readonly LoopbackModule _inputLoopback;
    private readonly LoopbackModule _outputLoopback;

    public LoopbackModule InputLoopback => _inputLoopback;
    public LoopbackModule OutputLoopback => _outputLoopback;
    private readonly IAppDataService _appDataService;
    private readonly IMixerService _mixerService;
    private readonly IMidiControllerSetupService _midiSetupService;
    private readonly int _layerId;
    private readonly ulong _midiControllerChannelId;

    public FilterChain? FilterChain
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public FilterGraph? FilterGraph
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
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

    public async Task AddFilterAction()
    {
        var dialogViewModel = new AddFilterChainViewModel(_appDataService);
        var dialog = new AddFilterChainView()
        {
            DataContext = dialogViewModel
        };

        await dialog.ShowDialog(WindowTools.GetMainWindow()!);

        if (dialogViewModel is
            { DialogResult: true, SelectedFilterGraph: not null })
        {
            var channelStrip = await _mixerService.AddFilterToChannelStrip(
                _layerId,
                ChannelId,
                dialogViewModel.SelectedFilterGraph);
            FilterGraph = dialogViewModel.SelectedFilterGraph;
            FilterChain = channelStrip.FilterChain;
        }
    }

    public void DeleteFilterAction()
    {
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

    private readonly ChannelType _channelType;
}