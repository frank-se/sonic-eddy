using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fr.Wireplumber.Model.Objects;
using SonicEddy.Contracts.FilterGraph;
using SonicEddy.Services.AppData;
using SonicEddy.Services.Preferences;
using SonicEddy.Services.Wireplumber;

namespace SonicEddy.Services.MixerServiceV2;

public class MixerService : IMixerService, IDisposable
{
    private readonly IWireplumberService _wireplumberService;
    private readonly IPreferenceService _preferenceService;
    private readonly MixerEditor _editor;

    public MixerService(IAppDataService appDataService,
        IWireplumberService wireplumberService,
        IPreferenceService preferenceService)
    {
        _wireplumberService = wireplumberService;
        _preferenceService = preferenceService;
        _editor = new(wireplumberService);

        _wireplumberService.NodeAdded += OnNodeAdded;
        _wireplumberService.NodeDeleted += OnNodeDeleted;
    }

    public Mixer? CurrentMixer { get; private set; }

    private readonly Lock _isModifyingLock = new Lock();
    private bool _isModifyingMixer;

    private List<ulong> _myNodeIds = [];

    private readonly SemaphoreSlim _externalChange = new(1, 1);
    private readonly SemaphoreSlim _internalChange = new(1, 1);

    public async Task<Mixer> NewCurrentMixer(string name)
    {
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
                CurrentMixer = await _editor.Create(_preferenceService.Preferences?.DefaultMasterOutputName);

                List<IEnumerable<ulong>> ids =
                [
                    CurrentMixer.Channels
                        .Select(c => c.FilterChain?.CaptureNode.ObjectSerial)
                        .OfType<ulong>(),
                    CurrentMixer.Channels
                        .Select(c => c.FilterChain?.PlaybackNode.ObjectSerial)
                        .OfType<ulong>(),
                    CurrentMixer.Channels
                        .Select(c => c.InputLoopback.CaptureNode.ObjectSerial),
                    CurrentMixer.Channels
                        .Select(c => c.InputLoopback.PlaybackNode.ObjectSerial),
                    CurrentMixer.Channels
                        .Select(c => c.OutputLoopback.CaptureNode.ObjectSerial),
                    CurrentMixer.Channels
                        .Select(c =>
                            c.OutputLoopback.PlaybackNode.ObjectSerial),
                    CurrentMixer.Channels
                        .SelectMany(c =>
                            c.SendLoopbacks.Select(s =>
                                s.CaptureNode.ObjectSerial)),
                    CurrentMixer.Channels
                        .SelectMany(c =>
                            c.SendLoopbacks.Select(s =>
                                s.PlaybackNode.ObjectSerial)),
                    CurrentMixer.SendReturns
                        .Select(r => r.FilterChain?.CaptureNode.ObjectSerial)
                        .OfType<ulong>(),
                    CurrentMixer.SendReturns
                        .Select(r => r.FilterChain?.PlaybackNode.ObjectSerial)
                        .OfType<ulong>(),
                    CurrentMixer.SendReturns
                        .Select(r => r.InputLoopback.CaptureNode.ObjectSerial),
                    CurrentMixer.SendReturns
                        .Select(r => r.InputLoopback.PlaybackNode.ObjectSerial),
                    CurrentMixer.SendReturns
                        .Select(r => r.OutputLoopback.CaptureNode.ObjectSerial),
                    CurrentMixer.SendReturns
                        .Select(r =>
                            r.OutputLoopback.PlaybackNode.ObjectSerial),
                    CurrentMixer.GroupChannels
                        .Select(c => c.FilterChain?.CaptureNode.ObjectSerial)
                        .OfType<ulong>(),
                    CurrentMixer.GroupChannels
                        .Select(c => c.FilterChain?.PlaybackNode.ObjectSerial)
                        .OfType<ulong>(),
                    CurrentMixer.GroupChannels
                        .Select(c => c.InputLoopback.CaptureNode.ObjectSerial),
                    CurrentMixer.GroupChannels
                        .Select(c => c.InputLoopback.PlaybackNode.ObjectSerial),
                    CurrentMixer.GroupChannels
                        .Select(c => c.OutputLoopback.CaptureNode.ObjectSerial),
                    CurrentMixer.GroupChannels
                        .Select(c =>
                            c.OutputLoopback.PlaybackNode.ObjectSerial),
                    CurrentMixer.GroupChannels
                        .SelectMany(c =>
                            c.SendLoopbacks.Select(s =>
                                s.CaptureNode.ObjectSerial)),
                    CurrentMixer.GroupChannels
                        .SelectMany(c =>
                            c.SendLoopbacks.Select(s =>
                                s.PlaybackNode.ObjectSerial)),
                    [
                        CurrentMixer.MasterChannel.FilterChain?.CaptureNode
                            .ObjectSerial ?? 0ul
                    ],
                    [
                        CurrentMixer.MasterChannel.FilterChain?.PlaybackNode
                            .ObjectSerial ?? 0ul
                    ],
                    [
                        CurrentMixer.MasterChannel.InputLoopback.CaptureNode
                            .ObjectSerial
                    ],
                    [
                        CurrentMixer.MasterChannel.InputLoopback.PlaybackNode
                            .ObjectSerial
                    ],
                    [
                        CurrentMixer.MasterChannel.OutputLoopback.CaptureNode
                            .ObjectSerial
                    ],
                    [
                        CurrentMixer.MasterChannel.OutputLoopback.PlaybackNode
                            .ObjectSerial
                    ],
                ];

                _myNodeIds = ids.SelectMany(i => i).ToList();
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

    public async Task<Mixer?> GetAndLock()
    {
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

    public async Task<ChannelStrip> AddFilterToChannelStrip(ulong channelId,
        FilterGraph filterGraph)
    {
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
                CurrentMixer = await _editor.AddFilterToChannelStrip(
                    CurrentMixer,
                    channelId,
                    filterGraph);

                var modifiedChannel =
                    CurrentMixer.Channels.First(c => c.ChannelId == channelId);

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

        return CurrentMixer.Channels.First(c => c.ChannelId == channelId);
    }

    public event Action<List<InputChannel>>? InputsChanged;
    public event Action<List<OutputChannel>>? OutputsChanged;

    private readonly Queue<Node> _pendingAddedNodes = [];

    private void OnNodeAdded(Node node)
    {
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
                    CurrentMixer.Inputs.Add(input);
                    inputsChanged = true;
                }
                else if (_wireplumberService.IsCaptureNode(node))
                {
                    var output =
                        new OutputChannel(node.Description ?? "Unknown", node);
                    CurrentMixer.Outputs.Add(output);
                    outputsChanged = true;
                }
            }
        }
        finally
        {
            _internalChange.Release();
        }

        if (inputsChanged) InputsChanged?.Invoke(CurrentMixer!.Inputs);
        if (outputsChanged) OutputsChanged?.Invoke(CurrentMixer!.Outputs);
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