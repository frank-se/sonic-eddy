using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fr.Sonic;
using Fr.Sonic.Model.Config;
using Fr.Sonic.Model.Config.FilterChain;
using Fr.Sonic.Model.Objects;
using Fr.Sonic.Modules.Models;
using Microsoft.Extensions.Logging;
using SonicEddy.Contracts.DrumMixer;
using SonicEddy.Services.AppData;
using SonicEddy.Services.Preferences;

namespace SonicEddy.Services.DrumMixer;

public sealed class DrumMixerService : IDrumMixerService, IDisposable
{
    public const string CaptureNodeName = "se.drum-mixer.input";
    public const string PlaybackNodeName = "se.drum-mixer.output";

    private readonly IAppDataService _appDataService;
    private readonly IPreferenceService _preferences;
    private readonly ILogger<DrumMixerService> _logger;
    private readonly SemaphoreSlim _storeLock = new(1, 1);
    private readonly object _stateLock = new();
    private readonly Dictionary<int, (ulong Output, ulong Input)> _ownedLinks =
        [];
    private CancellationTokenSource? _storeDebounce;
    private FilterChain? _filterChain;
    private bool _initialized;

    public DrumMixerService(IAppDataService appDataService,
        IPreferenceService preferences, ILogger<DrumMixerService> logger)
    {
        _appDataService = appDataService;
        _preferences = preferences;
        _logger = logger;
        FrSonic.NodeRegistry.Added += OnObjectChanged;
        FrSonic.NodeRegistry.Deleted += OnObjectChanged;
        FrSonic.PortRegistry.Added += OnObjectChanged;
        FrSonic.PortRegistry.Deleted += OnObjectChanged;
        FrSonic.LinkRegistry.Added += OnObjectChanged;
        FrSonic.LinkRegistry.Deleted += OnObjectChanged;
    }

    public bool IsConfigured =>
        _preferences.Preferences?.DrumMixerEnabled ?? false;
    public bool IsAvailable { get; private set; }
    public DrumMixerConfig State { get; private set; } = CreateDefaultState();
    public event Action? Changed;

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;

        if (_preferences.Preferences is null)
            await _preferences.Load();
        State = Normalize(await _appDataService.LoadDrumMixerConfig());

        if (!IsConfigured)
        {
            Changed?.Invoke();
            return;
        }

        try
        {
            VerifyPlugins();
            _filterChain = await FrSonic.ModuleFactory.CreateFilterChainAsync(
                "drum-mixer", new FilterChainModuleConfig
                {
                    MediaName = "Sonic Eddy Drum Mixer",
                    LinkGroup = "se.drum-mixer",
                    CaptureProps = new NodePropertiesConfig
                    {
                        Name = CaptureNodeName,
                        Description = "Sonic Eddy Drum Mixer Inputs",
                        Linger = true,
                        AutoConnect = false,
                        DontFallback = true,
                        Passive = false,
                        MediaClass = "Stream/Input/Audio",
                        AudioPosition = Enumerable.Range(0,
                            DrumMixerGraphBuilder.ChannelCount)
                            .Select(i => $"AUX{i}").ToList()
                    },
                    PlaybackProps = new NodePropertiesConfig
                    {
                        Name = PlaybackNodeName,
                        Description = "Sonic Eddy Drum Mixer",
                        Linger = true,
                        AutoConnect = false,
                        DontFallback = true,
                        Passive = false,
                        MediaClass = "Stream/Output/Audio",
                        AudioPosition = ["FL", "FR"]
                    },
                    FilterGraph = DrumMixerGraphBuilder.Build(State)
                });
            IsAvailable = true;
            ApplyAllRuntimeValues();
            ReconcileLinks();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to create drum mixer");
            _filterChain?.Destroy();
            _filterChain = null;
            IsAvailable = false;
        }

        Changed?.Invoke();
    }

    public IReadOnlyList<Port> GetSourcePorts() =>
        FrSonic.PortRegistry.Objects
            .Where(port => port.Direction == "out")
            .Where(port =>
            {
                var node = FrSonic.NodeRegistry.GetByObjectId(port.Node.Id);
                return node is not null &&
                       (node.Media.Class is "Audio/Source" or
                           "Stream/Output/Audio") &&
                       string.IsNullOrEmpty(node.Pmx.Tag) &&
                       node.Name != PlaybackNodeName;
            })
            .OrderBy(port =>
            {
                var node = FrSonic.NodeRegistry.GetByObjectId(port.Node.Id);
                return node?.Description ?? node?.Name;
            })
            .ThenBy(port => port.PortId)
            .ToList();

    public Port? ResolveSource(int channelIndex)
    {
        var channel = State.Channels[channelIndex];
        return GetSourcePorts().FirstOrDefault(port =>
            PortKey(port) == (channel.SourceNodeName, channel.SourcePortName));
    }

    public void SetSource(int channelIndex, Port? port)
    {
        var channel = State.Channels[channelIndex];
        if (port is null)
        {
            channel.SourceNodeName = null;
            channel.SourcePortName = null;
        }
        else
        {
            var key = PortKey(port);
            channel.SourceNodeName = key.NodeName;
            channel.SourcePortName = key.PortName;
        }

        ReconcileChannelLink(channelIndex);
        StateChanged();
    }

    public void SetChannelName(int channelIndex, string name)
    {
        State.Channels[channelIndex].Name = name;
        StateChanged();
    }

    public void SetChannelVolume(int channelIndex, float value)
    {
        State.Channels[channelIndex].Volume = value;
        SetBusGain("dry", channelIndex, value);
        StateChanged();
    }

    public void SetSend(int channelIndex, bool room, float value)
    {
        if (room)
            State.Channels[channelIndex].RoomSend = value;
        else
            State.Channels[channelIndex].PlateSend = value;
        SetBusGain(room ? "room" : "plate", channelIndex, value);
        StateChanged();
    }

    public void SetReturnVolume(bool room, float value)
    {
        (room ? State.Room : State.Plate).Volume = value;
        SetRuntimeParameter(room ? "final_l:Gain 2" : "final_l:Gain 3",
            value);
        SetRuntimeParameter(room ? "final_r:Gain 2" : "final_r:Gain 3",
            value);
        StateChanged();
    }

    public void SetParameter(int channelIndex, string name, float value)
    {
        SetParameterValue(State.Channels[channelIndex].Parameters, name, value);
        var separator = name.IndexOf(':');
        if (separator < 1) return;
        var plugin = name[..separator];
        var symbol = name[(separator + 1)..];
        SetRuntimeParameter(
            $"ch{channelIndex + 1:00}_{plugin}:{symbol}", value);
        StateChanged();
    }

    public void SetReturnParameter(bool room, string name, float value)
    {
        SetParameterValue((room ? State.Room : State.Plate).Parameters, name,
            value);
        SetRuntimeParameter($"{(room ? "room" : "plate")}_reverb:{name}",
            value);
        StateChanged();
    }

    private void VerifyPlugins()
    {
        var installed = FrSonicLv2.PluginDescriptions()
            .Select(plugin => plugin.Uri).ToHashSet();
        var missing = DrumMixerGraphBuilder.RequiredPluginUris
            .Where(uri => !installed.Contains(uri)).ToArray();
        if (missing.Length > 0)
            throw new InvalidOperationException(
                $"Missing required LV2 plugins: {string.Join(", ", missing)}");
    }

    private void ApplyAllRuntimeValues()
    {
        for (var i = 0; i < State.Channels.Count; i++)
        {
            SetChannelVolumeRuntime(i, State.Channels[i].Volume);
            SetBusGain("room", i, State.Channels[i].RoomSend);
            SetBusGain("plate", i, State.Channels[i].PlateSend);
            foreach (var parameter in State.Channels[i].Parameters)
            {
                var separator = parameter.Name.IndexOf(':');
                if (separator < 1) continue;
                SetRuntimeParameter(
                    $"ch{i + 1:00}_{parameter.Name[..separator]}:" +
                    parameter.Name[(separator + 1)..], parameter.Value);
            }
        }

        SetReturnVolumeRuntime(true, State.Room.Volume);
        SetReturnVolumeRuntime(false, State.Plate.Volume);
        foreach (var parameter in State.Room.Parameters)
            SetRuntimeParameter($"room_reverb:{parameter.Name}",
                parameter.Value);
        foreach (var parameter in State.Plate.Parameters)
            SetRuntimeParameter($"plate_reverb:{parameter.Name}",
                parameter.Value);
    }

    private void SetChannelVolumeRuntime(int channelIndex, float value) =>
        SetBusGain("dry", channelIndex, value);

    private void SetReturnVolumeRuntime(bool room, float value)
    {
        SetRuntimeParameter(room ? "final_l:Gain 2" : "final_l:Gain 3",
            value);
        SetRuntimeParameter(room ? "final_r:Gain 2" : "final_r:Gain 3",
            value);
    }

    private void SetBusGain(string bus, int channelIndex, float value)
    {
        var half = channelIndex < 8 ? "1_8" : "9_16";
        var gain = channelIndex % 8 + 1;
        SetRuntimeParameter($"{bus}_l_{half}:Gain {gain}", value);
        SetRuntimeParameter($"{bus}_r_{half}:Gain {gain}", value);
    }

    private void SetRuntimeParameter(string name, float value)
    {
        if (!IsAvailable || _filterChain is null) return;
        _filterChain.CaptureNode.SetParam(name, value);
    }

    private void ReconcileLinks()
    {
        for (var i = 0; i < DrumMixerGraphBuilder.ChannelCount; i++)
            ReconcileChannelLink(i);
    }

    private void ReconcileChannelLink(int channelIndex)
    {
        if (!IsAvailable || _filterChain is null) return;
        var inputs = FrSonic.PortRegistry.Objects
            .Where(port => port.Node.Id == _filterChain.CaptureNode.ObjectId &&
                           port.Direction == "in")
            .OrderBy(port => port.PortId).ToArray();
        var input = inputs.FirstOrDefault(port =>
                        port.Channel == $"AUX{channelIndex}") ??
                    inputs.ElementAtOrDefault(channelIndex);
        if (input is null) return;
        var desired = ResolveSource(channelIndex);

        if (_ownedLinks.TryGetValue(channelIndex, out var owned))
        {
            var existing = FrSonic.LinkRegistry.Objects.FirstOrDefault(candidate =>
                candidate.OutputPortId == owned.Output &&
                candidate.InputPortId == owned.Input);
            if (existing is null)
            {
                _ownedLinks.Remove(channelIndex);
            }
            else if (desired is null || owned.Output != desired.ObjectId ||
                     owned.Input != input.ObjectId)
            {
                FrSonic.LinkFactory.DeleteLink(existing);
                _ownedLinks.Remove(channelIndex);
            }
        }

        if (desired is not null &&
            !_ownedLinks.ContainsKey(channelIndex))
        {
            FrSonic.LinkFactory.CreateLink(desired, input);
            _ownedLinks[channelIndex] = (desired.ObjectId, input.ObjectId);
        }
    }

    private void StateChanged()
    {
        ScheduleStore();
        Changed?.Invoke();
    }

    private void ScheduleStore()
    {
        lock (_stateLock)
        {
            _storeDebounce?.Cancel();
            _storeDebounce?.Dispose();
            _storeDebounce = new();
            _ = StoreAfterDelay(_storeDebounce.Token);
        }
    }

    private async Task StoreAfterDelay(CancellationToken token)
    {
        try
        {
            await Task.Delay(350, token);
            await _storeLock.WaitAsync(token);
            try
            {
                await _appDataService.StoreDrumMixerConfig(State);
            }
            finally
            {
                _storeLock.Release();
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void OnObjectChanged<T>(T _)
    {
        if (!_initialized || !IsAvailable) return;
        ReconcileLinks();
        Changed?.Invoke();
    }

    private static (string? NodeName, string? PortName) PortKey(Port port)
    {
        var node = FrSonic.NodeRegistry.GetByObjectId(port.Node.Id);
        return (node?.Name, port.Name ?? port.Alias ?? port.Channel);
    }

    private static void SetParameterValue(
        List<DrumMixerParameterValue> parameters, string name, float value)
    {
        var parameter = parameters.FirstOrDefault(candidate =>
            candidate.Name == name);
        if (parameter is null)
            parameters.Add(new() { Name = name, Value = value });
        else
            parameter.Value = value;
    }

    private static DrumMixerConfig Normalize(DrumMixerConfig? state)
    {
        state ??= CreateDefaultState();
        while (state.Channels.Count < DrumMixerGraphBuilder.ChannelCount)
            state.Channels.Add(CreateDefaultChannel(state.Channels.Count));
        if (state.Channels.Count > DrumMixerGraphBuilder.ChannelCount)
            state.Channels.RemoveRange(DrumMixerGraphBuilder.ChannelCount,
                state.Channels.Count - DrumMixerGraphBuilder.ChannelCount);
        state.Room ??= CreateDefaultReturn();
        state.Plate ??= CreateDefaultReturn();
        EnsureReturnDefaults(state.Room);
        EnsureReturnDefaults(state.Plate);
        return state;
    }

    private static DrumMixerConfig CreateDefaultState() => new()
    {
        Channels = Enumerable.Range(0, DrumMixerGraphBuilder.ChannelCount)
            .Select(CreateDefaultChannel).ToList(),
        Room = CreateDefaultReturn(),
        Plate = CreateDefaultReturn()
    };

    private static DrumMixerChannelConfig CreateDefaultChannel(int index) =>
        new()
        {
            Name = $"Drum {index + 1}",
            Volume = 1.0f,
            Parameters =
            [
                new() { Name = "fil4:enable", Value = 0.0f },
                new() { Name = "transient:bypass", Value = 1.0f },
                new() { Name = "compressor:bypass", Value = 1.0f },
                new() { Name = "saturator:bypass", Value = 1.0f }
            ]
        };

    private static DrumMixerReturnConfig CreateDefaultReturn() => new()
    {
        Volume = 1.0f,
        Parameters =
        [
            new() { Name = "dry_level", Value = 0.0f }
        ]
    };

    private static void EnsureReturnDefaults(DrumMixerReturnConfig state)
    {
        if (state.Parameters.All(parameter => parameter.Name != "dry_level"))
            state.Parameters.Add(new() { Name = "dry_level", Value = 0.0f });
    }

    public void Dispose()
    {
        FrSonic.NodeRegistry.Added -= OnObjectChanged;
        FrSonic.NodeRegistry.Deleted -= OnObjectChanged;
        FrSonic.PortRegistry.Added -= OnObjectChanged;
        FrSonic.PortRegistry.Deleted -= OnObjectChanged;
        FrSonic.LinkRegistry.Added -= OnObjectChanged;
        FrSonic.LinkRegistry.Deleted -= OnObjectChanged;
        _storeDebounce?.Cancel();
        _storeDebounce?.Dispose();
        if (_initialized)
        {
            _storeLock.Wait();
            try
            {
                _appDataService.StoreDrumMixerConfig(State).GetAwaiter()
                    .GetResult();
            }
            finally
            {
                _storeLock.Release();
            }
        }
        _filterChain?.Destroy();
        _storeLock.Dispose();
    }
}
