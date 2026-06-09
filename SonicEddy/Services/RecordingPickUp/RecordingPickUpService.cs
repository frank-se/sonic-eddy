using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fr.Sonic;
using Fr.Sonic.Model.Config;
using Fr.Sonic.Model.Config.LoopbackModule;
using Fr.Sonic.Model.Objects;
using Fr.Sonic.Modules.Models;
using SonicEddy.Contracts.RecordingPickUp;
using SonicEddy.Services.AppData;
using SonicEddy.Services.Monitoring;
using SonicEddy.Services.Wireplumber;

namespace SonicEddy.Services.RecordingPickUp;

public class RecordingPickUpService(
    IAppDataService appDataService,
    IWireplumberService wireplumberService,
    IMonitoringLinkService monitoringLinkService)
    : IRecordingPickUpService, IDisposable
{
    private const string CaptureMediaClass = "Stream/Input/Audio";
    private const string PlaybackMediaClass = "Stream/Output/Audio";
    private const string NodeNamePrefix = "recording-pickup-";
    private static readonly List<string> StereoPosition = ["FL", "FR"];

    private readonly List<RecordingPickUpEntry> _configuredEntries = [];
    private readonly HashSet<Guid> _activePickUps = [];
    private readonly HashSet<Guid> _pickUpsBeingCreated = [];
    private readonly SemaphoreSlim _initializeLock = new(1, 1);
    private readonly SemaphoreSlim _restoreLock = new(1, 1);
    private readonly SemaphoreSlim _storeLock = new(1, 1);
    private readonly object _lock = new();
    private bool _initialized;

    public List<RecordingPickUp> PickUps { get; } = [];
    public event Action<RecordingPickUp>? Added;

    public async Task InitializeAsync()
    {
        await _initializeLock.WaitAsync();
        try
        {
            if (_initialized) return;

            var config = await appDataService.LoadRecordingPickUpConfig();
            lock (_lock)
                _configuredEntries.AddRange(config?.PickUps ?? []);

            wireplumberService.NodeAdded += OnNodeAdded;
            await ApplyConfiguredPickUpsAsync();
            _initialized = true;
        }
        finally
        {
            _initializeLock.Release();
        }
    }

    public async Task AddPickUp(string name, MonitoringChannelKey sourceKey,
        MonitoringSource sourcePosition, Node destNode)
    {
        await InitializeAsync();

        var id = Guid.NewGuid();
        var sourceNode = monitoringLinkService.ResolvePickUpNode(sourceKey, sourcePosition);
        if (sourceNode is null) return;

        await CreatePickUpAsync(id, name, sourceKey, sourcePosition, sourceNode, destNode, true, 0.0);

        lock (_lock)
            _configuredEntries.Add(new RecordingPickUpEntry
            {
                Id = id,
                Name = name,
                DestNodeName = destNode.Name ?? string.Empty,
                IsActive = true,
                Trim = 0.0,
                SourceLayerIndex = sourceKey.LayerIndex,
                SourceChannelType = (int)sourceKey.ChannelType,
                SourceChannelIndex = sourceKey.ChannelIndex,
                SourcePickUpPosition = (int)sourcePosition
            });

        await StoreConfigAsync();
    }

    public void SetActive(Guid id, bool active)
    {
        var pickUp = PickUps.FirstOrDefault(p => p.Id == id);
        if (pickUp is null) return;

        lock (_lock)
        {
            var entry = _configuredEntries.FirstOrDefault(e => e.Id == id);
            if (entry is not null) entry.IsActive = active;
        }

        ApplyVolume(pickUp.Loopback, active, GetTrim(id));
        _ = StoreConfigAsync();
    }

    public void SetTrim(Guid id, double trim)
    {
        var pickUp = PickUps.FirstOrDefault(p => p.Id == id);
        if (pickUp is null) return;

        lock (_lock)
        {
            var entry = _configuredEntries.FirstOrDefault(e => e.Id == id);
            if (entry is not null) entry.Trim = trim;
        }

        ApplyVolume(pickUp.Loopback, GetIsActive(id), trim);
        _ = StoreConfigAsync();
    }

    private static void ApplyVolume(LoopbackModule loopback, bool active, double trim)
    {
        var gain = active ? TrimToGain(trim) : 0.0;
        loopback.PlaybackNode.SetVolumes([gain, gain]);
    }

    private static double TrimToGain(double trim) =>
        trim <= 0.0 ? trim + 1.0 : 1.0 + trim * 3.0;

    private bool GetIsActive(Guid id)
    {
        lock (_lock)
            return _configuredEntries.FirstOrDefault(e => e.Id == id)?.IsActive ?? true;
    }

    private double GetTrim(Guid id)
    {
        lock (_lock)
            return _configuredEntries.FirstOrDefault(e => e.Id == id)?.Trim ?? 0.0;
    }

    private async Task CreatePickUpAsync(Guid id, string name,
        MonitoringChannelKey sourceKey, MonitoringSource sourcePosition,
        Node sourceNode, Node destNode, bool isActive, double trim)
    {
        var fullName = $"{NodeNamePrefix}{name}";

        var loopback = await wireplumberService.CreateLoopbackModule(
            fullName, new LoopbackModuleConfig
            {
                CaptureProps = new NodePropertiesConfig
                {
                    Linger = true,
                    Name = $"{fullName}-capture",
                    Description = $"{fullName}-capture",
                    DontFallback = true,
                    MediaClass = CaptureMediaClass,
                    TargetObject = sourceNode.ObjectSerial.ToString(),
                    AudioPosition = StereoPosition
                },
                PlaybackProps = new NodePropertiesConfig
                {
                    Linger = true,
                    Name = $"{fullName}-playback",
                    Description = $"{fullName}-playback",
                    DontFallback = true,
                    AutoConnect = false,
                    MediaClass = PlaybackMediaClass,
                    TargetObject = destNode.ObjectSerial.ToString(),
                    AudioPosition = StereoPosition
                }
            });

        var label = BuildSourceLabel(sourceKey, sourcePosition);
        var pickUp = new RecordingPickUp(id, name, isActive, trim, label, destNode, loopback);
        PickUps.Add(pickUp);
        lock (_lock)
            _activePickUps.Add(id);

        ApplyVolume(loopback, isActive, trim);
        Added?.Invoke(pickUp);
    }

    private async Task ApplyConfiguredPickUpsAsync()
    {
        await _restoreLock.WaitAsync();
        try
        {
            RecordingPickUpEntry[] pending;
            lock (_lock)
            {
                pending = _configuredEntries.Where(e =>
                        !_activePickUps.Contains(e.Id) &&
                        _pickUpsBeingCreated.Add(e.Id))
                    .ToArray();
            }

            foreach (var entry in pending)
            {
                try
                {
                    var sourceKey = new MonitoringChannelKey(
                        entry.SourceLayerIndex,
                        (MonitoringChannelType)entry.SourceChannelType,
                        entry.SourceChannelIndex);
                    var sourcePosition = (MonitoringSource)entry.SourcePickUpPosition;

                    var sourceNode = monitoringLinkService.ResolvePickUpNode(sourceKey, sourcePosition);
                    if (sourceNode is null) continue;

                    var destNode = FrSonic.NodeRegistry.Objects.FirstOrDefault(n =>
                        string.Equals(n.Name, entry.DestNodeName, StringComparison.Ordinal));
                    if (destNode is null) continue;

                    await CreatePickUpAsync(entry.Id, entry.Name, sourceKey, sourcePosition,
                        sourceNode, destNode, entry.IsActive, entry.Trim);
                }
                catch
                {
                    // Retain and retry when graph changes.
                }
                finally
                {
                    lock (_lock)
                        _pickUpsBeingCreated.Remove(entry.Id);
                }
            }
        }
        finally
        {
            _restoreLock.Release();
        }
    }

    private async Task StoreConfigAsync()
    {
        await _storeLock.WaitAsync();
        try
        {
            RecordingPickUpEntry[] entries;
            lock (_lock)
                entries = _configuredEntries.ToArray();

            await appDataService.StoreRecordingPickUpConfig(new RecordingPickUpConfig
            {
                PickUps = entries.ToList()
            });
        }
        finally
        {
            _storeLock.Release();
        }
    }

    internal static string BuildSourceLabel(MonitoringChannelKey key, MonitoringSource source)
    {
        var layer = $"L{key.LayerIndex + 1}";
        var channel = key.ChannelType switch
        {
            MonitoringChannelType.Strip  => $"CH{key.ChannelIndex + 1}",
            MonitoringChannelType.Group  => $"GR{key.ChannelIndex + 1}",
            MonitoringChannelType.Master => "MST",
            MonitoringChannelType.Return => $"RT{key.ChannelIndex + 1}",
            _                            => "?"
        };
        var pos = source switch
        {
            MonitoringSource.Pre          => "Pre",
            MonitoringSource.Post         => "Post",
            MonitoringSource.OutPreFader  => "Out Pre",
            MonitoringSource.OutPostFader => "Out Post",
            _                             => "?"
        };
        return $"{layer} / {channel} / {pos}";
    }

    private void OnNodeAdded(Node node) => _ = ApplyConfiguredPickUpsAsync();

    public void Dispose()
    {
        wireplumberService.NodeAdded -= OnNodeAdded;
        _initializeLock.Dispose();
        _restoreLock.Dispose();
        _storeLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
