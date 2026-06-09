using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fr.Sonic;
using Fr.Sonic.Model.Objects;
using SonicEddy.Contracts.VirtualOutputs;
using SonicEddy.Services.AppData;
using SonicEddy.Services.Wireplumber;

namespace SonicEddy.Services.VirtualOutputs;

public sealed class VirtualOutputService(
    IAppDataService appDataService,
    IWireplumberService wireplumberService)
    : IVirtualOutputService, IDisposable
{
    private const string CaptureNodeMediaClass = "Stream/Input/Audio";
    private const string PlaybackNodeMediaClass = "Stream/Output/Audio";
    private const string NodeNamePrefix = "virtual-output-";
    private const string CaptureNodeNameSuffix = "-capture";
    private static readonly List<string> StereoAudioPosition = ["FL", "FR"];

    public static bool IsVirtualOutputCaptureNode(Node node) =>
        node.Name?.StartsWith(NodeNamePrefix) == true &&
        node.Name.EndsWith(CaptureNodeNameSuffix);
    private readonly List<VirtualOutputConfig> _configuredOutputs = [];
    private readonly HashSet<Guid> _activeOutputs = [];
    private readonly HashSet<Guid> _outputsBeingCreated = [];
    private readonly SemaphoreSlim _initializeLock = new(1, 1);
    private readonly SemaphoreSlim _restoreLock = new(1, 1);
    private readonly SemaphoreSlim _storeLock = new(1, 1);
    private readonly object _lock = new();
    private bool _initialized;

    public List<VirtualOutput> VirtualOutputs { get; } = [];

    public async Task InitializeAsync()
    {
        await _initializeLock.WaitAsync();
        try
        {
            if (_initialized) return;

            var config = await appDataService.LoadVirtualOutputsConfig();
            lock (_lock)
                _configuredOutputs.AddRange(config?.Outputs ?? []);

            wireplumberService.NodeAdded += OnNodeAdded;
            FrSonic.PortRegistry.Added += OnPortAdded;
            await ApplyConfiguredOutputsAsync();
            _initialized = true;
        }
        finally
        {
            _initializeLock.Release();
        }
    }

    public async Task AddVirtualOutput(string name, Node node, Port[] ports)
    {
        await InitializeAsync();

        var id = Guid.NewGuid();
        await CreateVirtualOutputAsync(id, name, node, ports);

        lock (_lock)
            _configuredOutputs.Add(BuildConfig(id, name, node, ports));

        await StoreConfigAsync();
    }

    private async Task CreateVirtualOutputAsync(Guid id, string name,
        Node node, Port[] ports)
    {
        var fullName = $"virtual-output-{name}";
        var playbackNodeAudioPosition =
            ports.Select(port => port.Channel).OfType<string>().ToList();

        var loopback = await wireplumberService.CreateLoopbackModule(
            fullName, new()
            {
                CaptureProps = new()
                {
                    Linger = true,
                    Name = $"{fullName}-capture",
                    Description = $"{fullName}-capture",
                    AudioPosition = StereoAudioPosition,
                    MediaClass = CaptureNodeMediaClass,
                    DontFallback = true,
                    AutoConnect = false
                },
                PlaybackProps = new()
                {
                    Linger = true,
                    Name = $"{fullName}-playback",
                    Description = $"{fullName}-playback",
                    DontFallback = true,
                    MediaClass = PlaybackNodeMediaClass,
                    TargetObject = node.ObjectSerial.ToString(),
                    AudioPosition = playbackNodeAudioPosition
                }
            });

        var virtualOutput = new VirtualOutput(name, node, ports, loopback);
        VirtualOutputs.Add(virtualOutput);
        lock (_lock)
            _activeOutputs.Add(id);

        Added?.Invoke(virtualOutput);
    }

    public event Action<VirtualOutput>? Added;

    private async Task ApplyConfiguredOutputsAsync()
    {
        await _restoreLock.WaitAsync();
        try
        {
            VirtualOutputConfig[] pending;
            lock (_lock)
            {
                pending = _configuredOutputs.Where(config =>
                        !_activeOutputs.Contains(config.Id) &&
                        _outputsBeingCreated.Add(config.Id))
                    .ToArray();
            }

            foreach (var config in pending)
            {
                try
                {
                    var node = FrSonic.NodeRegistry.Objects.FirstOrDefault(
                        candidate => string.Equals(candidate.Name,
                            config.NodeName, StringComparison.Ordinal));
                    if (node is null) continue;

                    var nodePorts = FrSonic.PortRegistry.Objects
                        .Where(port => port.Node.Id == node.ObjectId).ToArray();
                    var ports = config.Ports
                        .Select(port => FindPort(nodePorts, port))
                        .ToArray();
                    if (ports.Any(port => port is null)) continue;

                    await CreateVirtualOutputAsync(config.Id, config.Name,
                        node, ports.Cast<Port>().ToArray());
                }
                catch
                {
                    // Retain the configuration and retry as the graph changes.
                }
                finally
                {
                    lock (_lock)
                        _outputsBeingCreated.Remove(config.Id);
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
            VirtualOutputConfig[] outputs;
            lock (_lock)
                outputs = _configuredOutputs.ToArray();

            await appDataService.StoreVirtualOutputsConfig(new()
            {
                Outputs = outputs.ToList()
            });
        }
        finally
        {
            _storeLock.Release();
        }
    }

    private static VirtualOutputConfig BuildConfig(Guid id, string name,
        Node node, IEnumerable<Port> ports) => new()
    {
        Id = id,
        Name = name,
        NodeName = node.Name ?? string.Empty,
        Ports = ports.Select(port => new VirtualOutputPortConfig
        {
            Name = port.Name ?? string.Empty,
            Alias = port.Alias ?? string.Empty,
            Channel = port.Channel ?? string.Empty
        }).ToList()
    };

    private static Port? FindPort(IEnumerable<Port> ports,
        VirtualOutputPortConfig config)
    {
        var candidates = ports.ToArray();
        if (!string.IsNullOrEmpty(config.Alias))
        {
            var byAlias = candidates.FirstOrDefault(port =>
                string.Equals(port.Alias, config.Alias,
                    StringComparison.Ordinal));
            if (byAlias is not null) return byAlias;
        }

        var byName = candidates.FirstOrDefault(port =>
            string.Equals(port.Name, config.Name, StringComparison.Ordinal));
        if (byName is not null) return byName;

        return candidates.FirstOrDefault(port =>
            string.Equals(port.Channel, config.Channel,
                StringComparison.Ordinal));
    }

    private void OnNodeAdded(Node node) => _ = ApplyConfiguredOutputsAsync();

    private void OnPortAdded(Port port) => _ = ApplyConfiguredOutputsAsync();

    public void Dispose()
    {
        wireplumberService.NodeAdded -= OnNodeAdded;
        FrSonic.PortRegistry.Added -= OnPortAdded;
        _initializeLock.Dispose();
        _restoreLock.Dispose();
        _storeLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
