using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fr.Sonic.Model.Objects;
using Microsoft.Extensions.Logging;
using SonicEddy.Contracts.FilterGraph;
using SonicEddy.Services.AppData;
using SonicEddy.Services.Midi;
using SonicEddy.Services.Preferences;
using SonicEddy.Services.Wireplumber;

namespace SonicEddy.Services.MixerServiceV2;

public class MixerService : IMixerService, IDisposable
{
    private readonly IWireplumberService _wireplumberService;
    private readonly IPreferenceService _preferenceService;
    private readonly MixerEditor _editor;
    private readonly ILogger<MixerService> _logger;

    public MixerService(IAppDataService appDataService,
        IWireplumberService wireplumberService,
        IPreferenceService preferenceService,
        ILogger<MixerService> logger)
    {
        _wireplumberService = wireplumberService;
        _preferenceService = preferenceService;
        _logger = logger;
        _editor = new(wireplumberService);

        _wireplumberService.NodeAdded += OnNodeAdded;
        _wireplumberService.NodeDeleted += OnNodeDeleted;
    }

    public Mixer? CurrentMixer { get; private set; }

    private readonly Lock _isModifyingLock = new Lock();
    private bool _isModifyingMixer;

    private readonly List<ulong> _myNodeIds = [];

    private readonly SemaphoreSlim _externalChange = new(1, 1);
    private readonly SemaphoreSlim _internalChange = new(1, 1);

    public async Task<Mixer> NewCurrentMixer(string name,
        bool createSecondLayer = true)
    {
        if (_logger.IsEnabled(LogLevel.Trace))
            _logger.LogTrace("NewCurrentMixer {Name}", name);

        if (_preferenceService.Preferences is null)
            await _preferenceService.Load();

        await _externalChange.WaitAsync();

        try
        {
            lock (_isModifyingLock)
            {
                _isModifyingMixer = true;
            }

            await _internalChange.WaitAsync();

            try
            {
                _myNodeIds.Clear();

                var prefs = _preferenceService.Preferences ?? new();
                var masterOutputName = prefs.DefaultMasterOutputName;
                var numberOfChannels = prefs.NumberOfChannels;
                var numberOfGroupChannels = prefs.NumberOfGroupChannels;
                var numberOfReturnChannels = prefs.NumberOfReturnChannels;

                NumberOfChannelsPerLayer = numberOfChannels;
                NumberOfChannels = numberOfChannels * (createSecondLayer ? 2 : 1);
                NumberOfGroupChannelsPerLayer = numberOfGroupChannels;
                NumberOfGroupChannels =
                    numberOfGroupChannels * (createSecondLayer ? 2 : 1);

                var firstLayer = await _editor.Create(masterOutputName, 0,
                    numberOfChannels, numberOfGroupChannels,
                    numberOfReturnChannels);

                var layerOneIds =
                    CollectMixerLayerNodeIds(firstLayer).ToArray();
                _myNodeIds.AddRange(layerOneIds);

                if (createSecondLayer)
                {
                    var secondLayer = await _editor.Create(masterOutputName, 1,
                        numberOfChannels, numberOfGroupChannels,
                        numberOfReturnChannels, layerOneIds);

                    _myNodeIds.AddRange(CollectMixerLayerNodeIds(secondLayer));

                    CurrentMixer = new([firstLayer, secondLayer]);
                }
                else
                {
                    CurrentMixer = new([firstLayer]);
                }
            }
            finally
            {
                _internalChange.Release();
            }

            lock (_isModifyingLock)
            {
                _isModifyingMixer = false;
            }

            await FinishPendingAddNodeEvents();
        }
        finally
        {
            _externalChange.Release();
        }

        return CurrentMixer;
    }

    private IEnumerable<ulong> CollectMixerLayerNodeIds(MixerLayer layer)
    {
        if (_logger.IsEnabled(LogLevel.Trace))
            _logger.LogTrace("CollectMixerLayerIds");

        List<IEnumerable<ulong>> ids =
        [
            layer.Channels
                .Select(c => c.FilterChain?.CaptureNode.ObjectSerial)
                .OfType<ulong>(),
            layer.Channels
                .Select(c => c.FilterChain?.PlaybackNode.ObjectSerial)
                .OfType<ulong>(),
            layer.Channels
                .Select(c => c.InputLoopback.CaptureNodeObjectSerial),
            layer.Channels
                .Select(c => c.InputLoopback.PlaybackNodeObjectSerial),
            layer.Channels
                .Select(c => c.PreFxLooper.CaptureNodeObjectSerial),
            layer.Channels
                .Select(c => c.PreFxLooper.PlaybackNodeObjectSerial),
            layer.Channels
                .Select(c => c.OutputLoopback.CaptureNodeObjectSerial),
            layer.Channels
                .Select(c => c.OutputLoopback.PlaybackNodeObjectSerial),
            layer.Channels
                .SelectMany(c =>
                    c.SendLoopbacks.Select(s =>
                        s.CaptureNodeObjectSerial)),
            layer.Channels
                .SelectMany(c =>
                    c.SendLoopbacks.Select(s =>
                        s.PlaybackNodeObjectSerial)),
            layer.SendReturns
                .Select(r => r.FilterChain?.CaptureNode.ObjectSerial)
                .OfType<ulong>(),
            layer.SendReturns
                .Select(r => r.FilterChain?.PlaybackNode.ObjectSerial)
                .OfType<ulong>(),
            layer.SendReturns
                .Select(r => r.InputLoopback.CaptureNodeObjectSerial),
            layer.SendReturns
                .Select(r => r.InputLoopback.PlaybackNodeObjectSerial),
            layer.SendReturns
                .Select(r => r.OutputLoopback.CaptureNodeObjectSerial),
            layer.SendReturns
                .Select(r => r.OutputLoopback.PlaybackNodeObjectSerial),
            layer.GroupChannels
                .Select(c => c.FilterChain?.CaptureNode.ObjectSerial)
                .OfType<ulong>(),
            layer.GroupChannels
                .Select(c => c.FilterChain?.PlaybackNode.ObjectSerial)
                .OfType<ulong>(),
            layer.GroupChannels
                .Select(c => c.InputLoopback.CaptureNodeObjectSerial),
            layer.GroupChannels
                .Select(c => c.InputLoopback.PlaybackNodeObjectSerial),
            layer.GroupChannels
                .Select(c => c.PreFxLooper.CaptureNodeObjectSerial),
            layer.GroupChannels
                .Select(c => c.PreFxLooper.PlaybackNodeObjectSerial),
            layer.GroupChannels
                .Select(c => c.OutputLoopback.CaptureNodeObjectSerial),
            layer.GroupChannels
                .Select(c => c.OutputLoopback.PlaybackNodeObjectSerial),
            layer.GroupChannels
                .SelectMany(c =>
                    c.SendLoopbacks.Select(s =>
                        s.CaptureNodeObjectSerial)),
            layer.GroupChannels
                .SelectMany(c =>
                    c.SendLoopbacks.Select(s =>
                        s.PlaybackNodeObjectSerial)),
            [
                layer.MasterChannel.FilterChain?.CaptureNode
                    .ObjectSerial ?? 0ul
            ],
            [
                layer.MasterChannel.FilterChain?.PlaybackNode
                    .ObjectSerial ?? 0ul
            ],
            [layer.MasterChannel.InputLoopback.CaptureNodeObjectSerial],
            [layer.MasterChannel.InputLoopback.PlaybackNodeObjectSerial],
            [layer.MasterChannel.PreFxLooper.CaptureNodeObjectSerial],
            [layer.MasterChannel.PreFxLooper.PlaybackNodeObjectSerial],
            [layer.MasterChannel.OutputLoopback.CaptureNodeObjectSerial],
            [layer.MasterChannel.OutputLoopback.PlaybackNodeObjectSerial],
        ];

        return ids.SelectMany(i => i);
    }

    public async Task<Mixer?> GetAndLock()
    {
        _logger.LogTrace("GetAndLock");

        if (CurrentMixer is null) return null;

        await _externalChange.WaitAsync();

        lock (_isModifyingLock)
        {
            _isModifyingMixer = true;
        }

        await _internalChange.WaitAsync();
        return CurrentMixer;
    }

    public async Task Unlock()
    {
        _logger.LogTrace("Unlock");

        _internalChange.Release();

        lock (_isModifyingLock)
        {
            _isModifyingMixer = false;
        }

        try
        {
            await FinishPendingAddNodeEvents();
        }
        finally
        {
            _externalChange.Release();
        }
    }

    public async Task<ChannelStrip> AddFilterToChannelStrip(int layerId,
        ulong channelId,
        FilterGraph filterGraph)
    {
        _logger.LogTrace("AddFilterToChannelStrip");

        await _externalChange.WaitAsync();

        try
        {
            lock (_isModifyingLock)
            {
                _isModifyingMixer = true;
            }

            if (CurrentMixer is null)
                throw new InvalidOperationException("CurrentMixer is null");

            await _internalChange.WaitAsync();

            try
            {
                CurrentMixer.Layers[layerId] =
                    await _editor.AddFilterToChannelStrip(
                        CurrentMixer.Layers[layerId],
                        channelId,
                        filterGraph);

                var modifiedChannel =
                    CurrentMixer.Layers[layerId].Channels
                        .First(c => c.ChannelId == channelId);

                List<ulong?> ids =
                [
                    modifiedChannel.FilterChain?.CaptureNode.ObjectSerial,
                    modifiedChannel.FilterChain?.PlaybackNode.ObjectSerial,
                ];

                _myNodeIds.AddRange(ids.OfType<ulong>());
            }
            finally
            {
                _internalChange.Release();
            }

            lock (_isModifyingLock)
            {
                _isModifyingMixer = false;
            }

            await FinishPendingAddNodeEvents();
        }
        finally
        {
            _externalChange.Release();
        }

        return CurrentMixer.Layers[layerId].Channels
            .First(c => c.ChannelId == channelId);
    }

    public event Action<List<InputChannel>>? InputsChanged;
    public event Action<List<OutputChannel>>? OutputsChanged;

    public int NumberOfChannelsPerLayer { get; private set; } = 8;
    public int NumberOfChannels { get; private set; } = 16;
    public int NumberOfGroupChannelsPerLayer { get; private set; } = 4;
    public int NumberOfGroupChannels { get; private set; } = 8;

    private readonly Queue<Node> _pendingAddedNodes = [];

    private void OnNodeAdded(Node node)
    {
        _logger.LogTrace("OnNodeAdded");

        lock (_isModifyingLock)
        {
            if (_isModifyingMixer)
            {
                _pendingAddedNodes.Enqueue(node);
                return;
            }
        }

        _ = ProcessNodeAddedEvent(node);
    }

    private async Task FinishPendingAddNodeEvents()
    {
        _logger.LogTrace("FinishPendingAddNodeEvents");

        Queue<Node> toProcess;
        lock (_isModifyingLock)
        {
            toProcess = new(_pendingAddedNodes);
            _pendingAddedNodes.Clear();
        }

        while (toProcess.Count > 0)
        {
            var e = toProcess.Dequeue();
            await ProcessNodeAddedEvent(e);
        }
    }

    private async Task ProcessNodeAddedEvent(Node node)
    {
        _logger.LogTrace("ProcessNodeAddedEvent");

        await _internalChange.WaitAsync();

        var inputsChanged = false;
        var outputsChanged = false;

        try
        {
            if (CurrentMixer is not null &&
                !_myNodeIds.Contains(node.ObjectSerial))
            {
                if (_wireplumberService.IsPlaybackNode(node))
                {
                    var input = new InputChannel(node.Description ?? "Unknown",
                        node);
                    CurrentMixer.Layers[0].Inputs.Add(input);
                    CurrentMixer.Layers[1].Inputs.Add(input);
                    inputsChanged = true;
                }
                else if (_wireplumberService.IsCaptureNode(node))
                {
                    var output =
                        new OutputChannel(node.Description ?? "Unknown", node);
                    CurrentMixer.Layers[0].Outputs.Add(output);
                    CurrentMixer.Layers[1].Outputs.Add(output);
                    outputsChanged = true;
                }
            }
        }
        finally
        {
            _internalChange.Release();
        }

        if (inputsChanged)
            InputsChanged?.Invoke(CurrentMixer!.Layers[0].Inputs);
        if (outputsChanged)
            OutputsChanged?.Invoke(CurrentMixer!.Layers[0].Outputs);
    }

    private void OnNodeDeleted(Node node)
    {
    }

    public void Dispose()
    {
        _wireplumberService.NodeAdded -= OnNodeAdded;
        _wireplumberService.NodeDeleted -= OnNodeDeleted;

        GC.SuppressFinalize(this);
    }
}
