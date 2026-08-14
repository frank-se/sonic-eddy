using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fr.Sonic.Model.Config;
using Fr.Sonic.Model.Config.LoopbackModule;
using Fr.Sonic.Model.Objects;
using Fr.Sonic.Modules.Models;
using Microsoft.Extensions.Logging;
using SonicEddy.Contracts.FilterGraph;
using SonicEddy.Services.AppData;
using SonicEddy.Services.ExternalEffects;
using SonicEddy.Services.DrumMixer;
using SonicEddy.Services.Midi;
using SonicEddy.Services.JackInputPorts;
using SonicEddy.Services.VirtualInputs;
using SonicEddy.Services.VirtualOutputs;
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
        IExternalEffectService externalEffectService,
        ILogger<MixerService> logger)
    {
        _wireplumberService = wireplumberService;
        _preferenceService = preferenceService;
        _logger = logger;
        _editor = new(wireplumberService, externalEffectService);

        _wireplumberService.NodeAdded += OnNodeAdded;
        _wireplumberService.NodeDeleted += OnNodeDeleted;
    }

    public Mixer? CurrentMixer { get; private set; }

    private readonly Lock _isModifyingLock = new Lock();
    private bool _isModifyingMixer;

    private readonly List<ulong> _myNodeIds = [];

    private readonly SemaphoreSlim _externalChange = new(1, 1);
    private readonly SemaphoreSlim _internalChange = new(1, 1);

    public async Task<Mixer> NewCurrentMixer(string name)
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
                DestroyInsertProcessors(CurrentMixer);
                _myNodeIds.Clear();

                var prefs = _preferenceService.Preferences ?? new();
                var masterOutputName = prefs.DefaultMasterOutputName;
                var numberOfChannels = prefs.NumberOfChannels;
                var numberOfGroupChannels = prefs.NumberOfGroupChannels;
                var numberOfReturnChannels = prefs.NumberOfReturnChannels;

                NumberOfChannelsPerLayer = numberOfChannels;
                NumberOfChannels = numberOfChannels * 2;
                NumberOfGroupChannelsPerLayer = numberOfGroupChannels;
                NumberOfGroupChannels = numberOfGroupChannels * 2;

                // Create the GlobalMaster FilterChain first so master channels
                // target it at creation time — no post-hoc override needed.
                var physicalOutput = _wireplumberService.GetCaptureNodes()
                    .FirstOrDefault(n => n.Name == masterOutputName)
                    ?? _wireplumberService.GetCaptureNodes().First();

                var xfade = await _editor.CreateGlobalMasterFilterChain(
                    physicalOutput.ObjectSerial.ToString());

                var globalMasterCaptureSerial =
                    xfade.CaptureNode.ObjectSerial.ToString();
                var globalMaster = new GlobalMasterChannel(xfade,
                    physicalOutput);

                _myNodeIds.Add(xfade.CaptureNode.ObjectSerial);
                _myNodeIds.Add(xfade.PlaybackNode.ObjectSerial);

                MicChannel? micChannel = null;
                MicChannel? micChannel2 = null;
                if (prefs.MicChannelEnabled)
                {
                    micChannel =
                        await _editor.CreateMicChannel(xfade.CaptureNode, 1);

                    _myNodeIds.Add(
                        micChannel.InputLoopback.CaptureNodeObjectSerial);
                    _myNodeIds.Add(
                        micChannel.InputLoopback.PlaybackNodeObjectSerial);

                    micChannel2 =
                        await _editor.CreateMicChannel(xfade.CaptureNode, 2);

                    _myNodeIds.Add(
                        micChannel2.InputLoopback.CaptureNodeObjectSerial);
                    _myNodeIds.Add(
                        micChannel2.InputLoopback.PlaybackNodeObjectSerial);
                }

                var globalReturn1 =
                    await _editor.CreateGlobalReturnChannel(xfade.CaptureNode, 1);
                var globalReturn2 =
                    await _editor.CreateGlobalReturnChannel(xfade.CaptureNode, 2);

                _myNodeIds.Add(
                    globalReturn1.InputLoopback.CaptureNodeObjectSerial);
                _myNodeIds.Add(
                    globalReturn1.InputLoopback.PlaybackNodeObjectSerial);
                _myNodeIds.Add(
                    globalReturn1.OutputLoopback.CaptureNodeObjectSerial);
                _myNodeIds.Add(
                    globalReturn1.OutputLoopback.PlaybackNodeObjectSerial);
                _myNodeIds.Add(
                    globalReturn2.InputLoopback.CaptureNodeObjectSerial);
                _myNodeIds.Add(
                    globalReturn2.InputLoopback.PlaybackNodeObjectSerial);
                _myNodeIds.Add(
                    globalReturn2.OutputLoopback.CaptureNodeObjectSerial);
                _myNodeIds.Add(
                    globalReturn2.OutputLoopback.PlaybackNodeObjectSerial);

                var firstLayer = await _editor.Create(masterOutputName, 0,
                    numberOfChannels, numberOfGroupChannels,
                    numberOfReturnChannels, globalMasterCaptureSerial,
                    globalReturn1, globalReturn2);

                var layerOneIds =
                    CollectMixerLayerNodeIds(firstLayer).ToArray();
                _myNodeIds.AddRange(layerOneIds);

                LoopbackModule? monitoringLoopback = null;
                if (prefs.MonitoringChannelEnabled &&
                    prefs.DefaultMonitorOutputName is not null)
                {
                    monitoringLoopback =
                        await CreateMonitoringLoopback(
                            prefs.DefaultMonitorOutputName);
                    if (monitoringLoopback is not null)
                    {
                        _myNodeIds.Add(
                            monitoringLoopback.CaptureNodeObjectSerial);
                        _myNodeIds.Add(
                            monitoringLoopback.PlaybackNodeObjectSerial);
                    }
                }

                var secondLayer = await _editor.Create(masterOutputName, 1,
                    numberOfChannels, numberOfGroupChannels,
                    numberOfReturnChannels, globalMasterCaptureSerial,
                    globalReturn1, globalReturn2, layerOneIds);

                _myNodeIds.AddRange(CollectMixerLayerNodeIds(secondLayer));

                string? cueOutputSerial = null;
                if (prefs.DefaultCueOutputName is not null)
                {
                    var cueOutput = _wireplumberService.GetCaptureNodes()
                        .FirstOrDefault(n => n.Name == prefs.DefaultCueOutputName);
                    if (cueOutput is not null)
                        cueOutputSerial = cueOutput.ObjectSerial.ToString();
                }

                var cueFc = await _editor.CreateCueFilterChain(
                    globalMasterCaptureSerial, cueOutputSerial);
                var cue = new CueChannel(cueFc);
                _myNodeIds.Add(cueFc.CaptureNode.ObjectSerial);
                _myNodeIds.Add(cueFc.PlaybackNode.ObjectSerial);

                CurrentMixer = new([firstLayer, secondLayer],
                    monitoringLoopback, globalMaster, cue, micChannel,
                    micChannel2, globalReturn1, globalReturn2);
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

    private async Task<LoopbackModule?> CreateMonitoringLoopback(
        string monitorOutputName)
    {
        var monitorOutput = _wireplumberService.GetCaptureNodes()
            .FirstOrDefault(n => n.Name == monitorOutputName);

        if (monitorOutput is null)
            return null;

        return await _wireplumberService.CreateLoopbackModule(
            "mixer-monitoring", new LoopbackModuleConfig
            {
                CaptureProps = new NodePropertiesConfig
                {
                    Name = "mixer-monitoring-capture",
                    Description = "Monitoring Capture",
                    Linger = true,
                    AutoConnect = false,
                    DontFallback = true,
                    Passive = true,
                    MediaClass = "Stream/Input/Audio",
                    AudioPosition = ["FL", "FR"]
                },
                PlaybackProps = new NodePropertiesConfig
                {
                    Name = "mixer-monitoring-playback",
                    Description = "Monitoring Playback",
                    Linger = true,
                    AutoConnect = true,
                    DontFallback = true,
                    Passive = false,
                    TargetObject = monitorOutput.ObjectSerial.ToString(),
                    MediaClass = "Stream/Output/Audio",
                    AudioPosition = ["FL", "FR"]
                }
            });
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

        var result = CurrentMixer.Layers[layerId].Channels
            .First(c => c.ChannelId == channelId);
        _logger.LogDebug(
            "AddFilterToChannelStrip result channel={ChannelId} filterChainNull={FilterChainNull} captureSerial={CaptureSerial}",
            channelId, result.FilterChain is null,
            result.FilterChain?.CaptureNode.ObjectSerial);
        return result;
    }

    public async Task<FilterChain?> AddFilterToGroupChannel(int layerId,
        ulong channelId,
        FilterGraph filterGraph)
    {
        _logger.LogTrace("AddFilterToGroupChannel");

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
                    await _editor.AddFilterToGroupChannel(
                        CurrentMixer.Layers[layerId],
                        channelId,
                        filterGraph);

                var fc = CurrentMixer.Layers[layerId].GroupChannels
                    .First(c => c.ChannelId == channelId).FilterChain;

                List<ulong?> ids =
                [
                    fc?.CaptureNode.ObjectSerial,
                    fc?.PlaybackNode.ObjectSerial,
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

        var resultFc = CurrentMixer.Layers[layerId].GroupChannels
            .First(c => c.ChannelId == channelId).FilterChain;
        _logger.LogDebug(
            "AddFilterToGroupChannel result channel={ChannelId} filterChainNull={FilterChainNull} captureSerial={CaptureSerial}",
            channelId, resultFc is null, resultFc?.CaptureNode.ObjectSerial);
        return resultFc;
    }

    public async Task<FilterChain?> AddFilterToMasterChannel(int layerId,
        FilterGraph filterGraph)
    {
        _logger.LogTrace("AddFilterToMasterChannel");

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
                    await _editor.AddFilterToMasterChannel(
                        CurrentMixer.Layers[layerId],
                        filterGraph);

                var fc = CurrentMixer.Layers[layerId].MasterChannel.FilterChain;

                List<ulong?> ids =
                [
                    fc?.CaptureNode.ObjectSerial,
                    fc?.PlaybackNode.ObjectSerial,
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

        var masterFc = CurrentMixer.Layers[layerId].MasterChannel.FilterChain;
        _logger.LogDebug(
            "AddFilterToMasterChannel result layer={LayerId} filterChainNull={FilterChainNull} captureSerial={CaptureSerial}",
            layerId, masterFc is null, masterFc?.CaptureNode.ObjectSerial);
        return masterFc;
    }

    public Task RemoveFilterFromChannelStrip(int layerId, ulong channelId) =>
        ModifyLayer(layerId, layer =>
            Task.FromResult(_editor.RemoveFilterFromChannelStrip(
                layer, channelId)));

    public Task RemoveFilterFromGroupChannel(int layerId, ulong channelId) =>
        ModifyLayer(layerId, layer =>
            Task.FromResult(_editor.RemoveFilterFromGroupChannel(
                layer, channelId)));

    public Task RemoveFilterFromMasterChannel(int layerId) =>
        ModifyLayer(layerId, layer =>
            Task.FromResult(_editor.RemoveFilterFromMasterChannel(layer)));

    public async Task<FilterChain?> AddFilterToReturnChannel(int layerId,
        int index, FilterGraph filterGraph)
    {
        await ModifyLayer(layerId, layer =>
            _editor.AddFilterToReturnChannel(layer, index, filterGraph));
        return CurrentMixer?.Layers[layerId].SendReturns[index].FilterChain;
    }

    public Task RemoveFilterFromReturnChannel(int layerId, int index) =>
        ModifyLayer(layerId, layer =>
            Task.FromResult(_editor.RemoveFilterFromReturnChannel(
                layer, index)));

    public async Task<InsertProcessor?> AddExternalEffectToChannelStrip(
        int layerId, ulong channelId, Guid effectId)
    {
        await ModifyLayer(layerId, layer =>
            _editor.AddExternalEffectToChannelStrip(layer, channelId, effectId));
        return CurrentMixer?.Layers[layerId].Channels.First(channel =>
            channel.ChannelId == channelId).InsertProcessor;
    }

    public async Task<InsertProcessor?> AddExternalEffectToGroupChannel(
        int layerId, ulong channelId, Guid effectId)
    {
        await ModifyLayer(layerId, layer =>
            _editor.AddExternalEffectToGroupChannel(layer, channelId, effectId));
        return CurrentMixer?.Layers[layerId].GroupChannels.First(channel =>
            channel.ChannelId == channelId).InsertProcessor;
    }

    public async Task<InsertProcessor?> AddExternalEffectToMasterChannel(
        int layerId, Guid effectId)
    {
        await ModifyLayer(layerId, layer =>
            _editor.AddExternalEffectToMasterChannel(layer, effectId));
        return CurrentMixer?.Layers[layerId].MasterChannel.InsertProcessor;
    }

    public async Task<InsertProcessor?> AddExternalEffectToReturnChannel(
        int layerId, int index, Guid effectId)
    {
        await ModifyLayer(layerId, layer =>
            _editor.AddExternalEffectToReturnChannel(layer, index, effectId));
        return CurrentMixer?.Layers[layerId].SendReturns[index].InsertProcessor;
    }

    public async Task<InsertProcessor?> SetGlobalMasterInsertProcessor(
        InsertProcessorRequest? request)
    {
        await _externalChange.WaitAsync();
        try
        {
            if (CurrentMixer?.GlobalMaster is not { } globalMaster)
                return null;
            await _internalChange.WaitAsync();
            try
            {
                CurrentMixer = CurrentMixer with
                {
                    GlobalMaster = request switch
                    {
                        FilterChainInsertRequest filter =>
                            await _editor.AddFilterToGlobalMaster(globalMaster,
                                filter.FilterGraph),
                        ExternalEffectInsertRequest external =>
                            await _editor.AddExternalEffectToGlobalMaster(
                                globalMaster, external.ExternalEffectId),
                        null => _editor.RemoveGlobalMasterInsert(globalMaster),
                        _ => throw new ArgumentOutOfRangeException(
                            nameof(request))
                    }
                };
                RegisterProcessorNodes(
                    CurrentMixer.GlobalMaster.InsertProcessor);
            }
            finally
            {
                _internalChange.Release();
            }
        }
        finally
        {
            _externalChange.Release();
        }
        return CurrentMixer?.GlobalMaster?.InsertProcessor;
    }

    private void RegisterProcessorNodes(InsertProcessor? processor)
    {
        if (processor is null) return;
        if (!_myNodeIds.Contains(processor.InputNode.ObjectSerial))
            _myNodeIds.Add(processor.InputNode.ObjectSerial);
        if (!_myNodeIds.Contains(processor.OutputNode.ObjectSerial))
            _myNodeIds.Add(processor.OutputNode.ObjectSerial);
    }

    public void SetGlobalMasterOutputTarget(ulong objectSerial)
    {
        if (CurrentMixer?.GlobalMaster is not { } globalMaster) return;
        var node = Fr.Sonic.FrSonic.NodeRegistry.GetByObjectSerial(objectSerial);
        if (node is null) return;
        if (globalMaster.InsertProcessor is { } processor)
            processor.OutputNode.OverrideTargetObject(objectSerial.ToString());
        else
            globalMaster.CrossFader.PlaybackNode.OverrideTargetObject(
                objectSerial.ToString());
        CurrentMixer = CurrentMixer with
        {
            GlobalMaster = globalMaster with { OutputTargetObject = node }
        };
    }

    public async Task<FilterChain?> AddFilterToMicChannel(int micIndex,
        FilterGraph filterGraph)
    {
        _logger.LogTrace("AddFilterToMicChannel");

        await _externalChange.WaitAsync();
        try
        {
            lock (_isModifyingLock)
            {
                _isModifyingMixer = true;
            }

            if (GetMicChannel(micIndex) is not { } mic ||
                CurrentMixer?.GlobalMaster is not { } globalMaster)
                return null;

            await _internalChange.WaitAsync();
            try
            {
                var updated =
                    await _editor.AddFilterToMicChannel(mic,
                        globalMaster.CrossFader.CaptureNode, filterGraph,
                        micIndex);
                CurrentMixer = SetMicChannel(micIndex, updated);

                List<ulong?> ids =
                [
                    updated.FilterChain?.CaptureNode.ObjectSerial,
                    updated.FilterChain?.PlaybackNode.ObjectSerial,
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

        return GetMicChannel(micIndex)?.FilterChain;
    }

    public async Task RemoveFilterFromMicChannel(int micIndex)
    {
        await _externalChange.WaitAsync();
        try
        {
            lock (_isModifyingLock)
            {
                _isModifyingMixer = true;
            }

            if (GetMicChannel(micIndex) is { } mic &&
                CurrentMixer?.GlobalMaster is { } globalMaster)
            {
                await _internalChange.WaitAsync();
                try
                {
                    var updated = await _editor.RemoveFilterFromMicChannel(
                        mic, globalMaster.CrossFader.CaptureNode);
                    CurrentMixer = SetMicChannel(micIndex, updated);
                }
                finally
                {
                    _internalChange.Release();
                }
            }

            lock (_isModifyingLock)
            {
                _isModifyingMixer = false;
            }
        }
        finally
        {
            _externalChange.Release();
        }
    }

    public async Task<InsertProcessor?> AddExternalEffectToMicChannel(
        int micIndex, Guid effectId)
    {
        await _externalChange.WaitAsync();
        try
        {
            lock (_isModifyingLock)
            {
                _isModifyingMixer = true;
            }

            if (GetMicChannel(micIndex) is { } mic &&
                CurrentMixer?.GlobalMaster is { } globalMaster)
            {
                await _internalChange.WaitAsync();
                try
                {
                    var updated = await _editor.AddExternalEffectToMicChannel(
                        mic, globalMaster.CrossFader.CaptureNode, effectId,
                        micIndex);
                    CurrentMixer = SetMicChannel(micIndex, updated);
                    RegisterProcessorNodes(updated.InsertProcessor);
                }
                finally
                {
                    _internalChange.Release();
                }
            }

            lock (_isModifyingLock)
            {
                _isModifyingMixer = false;
            }
        }
        finally
        {
            _externalChange.Release();
        }

        return GetMicChannel(micIndex)?.InsertProcessor;
    }

    public async Task<FilterChain?> AddFilterToGlobalReturnChannel(int index,
        FilterGraph filterGraph)
    {
        await _externalChange.WaitAsync();
        try
        {
            lock (_isModifyingLock)
            {
                _isModifyingMixer = true;
            }

            if (GetGlobalReturnChannel(index) is not { } channel)
                return null;

            await _internalChange.WaitAsync();
            try
            {
                var updated = await _editor.AddFilterToGlobalReturnChannel(
                    channel, filterGraph, index);
                CurrentMixer = SetGlobalReturnChannel(index, updated);

                List<ulong?> ids =
                [
                    updated.FilterChain?.CaptureNode.ObjectSerial,
                    updated.FilterChain?.PlaybackNode.ObjectSerial,
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

        return GetGlobalReturnChannel(index)?.FilterChain;
    }

    public async Task RemoveFilterFromGlobalReturnChannel(int index)
    {
        await _externalChange.WaitAsync();
        try
        {
            lock (_isModifyingLock)
            {
                _isModifyingMixer = true;
            }

            if (GetGlobalReturnChannel(index) is { } channel)
            {
                await _internalChange.WaitAsync();
                try
                {
                    var updated =
                        _editor.RemoveFilterFromGlobalReturnChannel(channel);
                    CurrentMixer = SetGlobalReturnChannel(index, updated);
                }
                finally
                {
                    _internalChange.Release();
                }
            }

            lock (_isModifyingLock)
            {
                _isModifyingMixer = false;
            }
        }
        finally
        {
            _externalChange.Release();
        }
    }

    public async Task<InsertProcessor?> AddExternalEffectToGlobalReturnChannel(
        int index, Guid effectId)
    {
        await _externalChange.WaitAsync();
        try
        {
            lock (_isModifyingLock)
            {
                _isModifyingMixer = true;
            }

            if (GetGlobalReturnChannel(index) is { } channel)
            {
                await _internalChange.WaitAsync();
                try
                {
                    var updated =
                        await _editor.AddExternalEffectToGlobalReturnChannel(
                            channel, effectId, index);
                    CurrentMixer = SetGlobalReturnChannel(index, updated);
                    RegisterProcessorNodes(updated.InsertProcessor);
                }
                finally
                {
                    _internalChange.Release();
                }
            }

            lock (_isModifyingLock)
            {
                _isModifyingMixer = false;
            }
        }
        finally
        {
            _externalChange.Release();
        }

        return GetGlobalReturnChannel(index)?.InsertProcessor;
    }

    private ReturnChannel? GetGlobalReturnChannel(int index) =>
        index == 1 ? CurrentMixer?.GlobalReturn1 : CurrentMixer?.GlobalReturn2;

    private Mixer? SetGlobalReturnChannel(int index, ReturnChannel updated) =>
        index == 1
            ? CurrentMixer! with { GlobalReturn1 = updated }
            : CurrentMixer! with { GlobalReturn2 = updated };

    private MicChannel? GetMicChannel(int micIndex) =>
        micIndex == 1 ? CurrentMixer?.Mic : CurrentMixer?.Mic2;

    private Mixer? SetMicChannel(int micIndex, MicChannel updated) =>
        micIndex == 1
            ? CurrentMixer! with { Mic = updated }
            : CurrentMixer! with { Mic2 = updated };

    private async Task ModifyLayer(int layerId,
        Func<MixerLayer, Task<MixerLayer>> update)
    {
        await _externalChange.WaitAsync();
        try
        {
            lock (_isModifyingLock)
                _isModifyingMixer = true;

            try
            {
                if (CurrentMixer is null)
                    throw new InvalidOperationException(
                        "CurrentMixer is null");

                await _internalChange.WaitAsync();
                try
                {
                    CurrentMixer.Layers[layerId] =
                        await update(CurrentMixer.Layers[layerId]);
                    RegisterFilterNodes(CurrentMixer.Layers[layerId]);
                }
                finally
                {
                    _internalChange.Release();
                }

            }
            finally
            {
                lock (_isModifyingLock)
                    _isModifyingMixer = false;
            }

            await FinishPendingAddNodeEvents();
        }
        finally
        {
            _externalChange.Release();
        }
    }

    private void RegisterFilterNodes(MixerLayer layer)
    {
        IEnumerable<InsertProcessor?> processors =
        [
            layer.MasterChannel.InsertProcessor,
            .. layer.Channels.Select(channel => channel.InsertProcessor),
            .. layer.GroupChannels.Select(channel => channel.InsertProcessor),
            .. layer.SendReturns.Select(channel => channel.InsertProcessor)
        ];

        foreach (var processor in processors)
            RegisterProcessorNodes(processor);
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

    private static bool IsInternalNode(Node node) =>
        node.Name?.StartsWith("silence-") == true ||
        node.Name?.StartsWith("monitor ") == true ||
        (!string.IsNullOrEmpty(node.Pmx.Tag) &&
         node.Name != DrumMixerService.PlaybackNodeName &&
         !VirtualInputService.IsVirtualInputPlaybackNode(node) &&
         !VirtualOutputService.IsVirtualOutputCaptureNode(node) &&
         !JackInputPortService.IsJackInputPortPlaybackNode(node));

    private async Task ProcessNodeAddedEvent(Node node)
    {
        _logger.LogTrace("ProcessNodeAddedEvent");

        await _internalChange.WaitAsync();

        var inputsChanged = false;
        var outputsChanged = false;

        try
        {
            if (CurrentMixer is not null &&
                !_myNodeIds.Contains(node.ObjectSerial) &&
                !IsInternalNode(node))
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
        DestroyInsertProcessors(CurrentMixer);

        GC.SuppressFinalize(this);
    }

    private static void DestroyInsertProcessors(Mixer? mixer)
    {
        if (mixer is null) return;
        foreach (var processor in mixer.Layers.SelectMany(layer =>
                     layer.Channels.Select(channel => channel.InsertProcessor)
                         .Concat(layer.GroupChannels.Select(channel =>
                             channel.InsertProcessor))
                         .Concat(layer.SendReturns.Select(channel =>
                             channel.InsertProcessor))
                         .Append(layer.MasterChannel.InsertProcessor))
                 .OfType<InsertProcessor>())
            processor.Destroy();
        mixer.GlobalMaster?.InsertProcessor?.Destroy();
    }
}
