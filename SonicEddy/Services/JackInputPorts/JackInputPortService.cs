using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fr.Sonic;
using Fr.Sonic.Model.Config;
using Fr.Sonic.Model.Config.LoopbackModule;
using Fr.Sonic.Model.Objects;
using SonicEddy.Contracts.JackInputPorts;
using SonicEddy.Services.AppData;
using SonicEddy.Services.Wireplumber;

namespace SonicEddy.Services.JackInputPorts;

public sealed class JackInputPortService(
    IAppDataService appDataService,
    IWireplumberService wireplumberService)
    : IJackInputPortService, IDisposable
{
    private const string CaptureNodeMediaClass = "Stream/Input/Audio";
    private const string PlaybackNodeMediaClass = "Stream/Output/Audio";
    private const string NodeNamePrefix = "jack-input-";
    private const string CaptureNodeNameSuffix = "-capture";
    private const string PlaybackNodeNameSuffix = "-playback";
    private static readonly List<string> StereoAudioPosition = ["FL", "FR"];

    public static bool IsJackInputPortPlaybackNode(Node node) =>
        node.Name?.StartsWith(NodeNamePrefix) == true &&
        node.Name.EndsWith(PlaybackNodeNameSuffix);

    private static bool IsJackInputPortCaptureNode(Node node) =>
        node.Name?.StartsWith(NodeNamePrefix) == true &&
        node.Name.EndsWith(CaptureNodeNameSuffix);

    private readonly List<JackInputPortConfig> _configuredPorts = [];
    private readonly Dictionary<Guid, JackInputPort> _activePorts = [];
    private readonly HashSet<Guid> _portsBeingCreated = [];
    private readonly SemaphoreSlim _initializeLock = new(1, 1);
    private readonly SemaphoreSlim _restoreLock = new(1, 1);
    private readonly SemaphoreSlim _storeLock = new(1, 1);
    private readonly object _lock = new();
    private bool _initialized;

    public List<JackInputPort> JackInputPorts { get; } = [];

    public event Action<JackInputPort>? Added;
    public event Action<JackInputPort>? Deleted;

    public async Task InitializeAsync()
    {
        await _initializeLock.WaitAsync();
        try
        {
            if (_initialized) return;

            var config = await appDataService.LoadJackInputPortsConfig();
            lock (_lock)
                _configuredPorts.AddRange(config?.Ports ?? []);

            wireplumberService.NodeAdded += OnNodeAdded;
            FrSonic.PortRegistry.Added += OnPortAdded;
            await ApplyConfiguredPortsAsync();
            _initialized = true;
        }
        finally
        {
            _initializeLock.Release();
        }
    }

    public async Task AddJackInputPort(string name, string clientName,
        string[] jackPortNames)
    {
        await InitializeAsync();

        var id = Guid.NewGuid();
        var port = await CreatePortAsync(id, name, clientName, jackPortNames);

        lock (_lock)
            _configuredPorts.Add(new JackInputPortConfig
            {
                Id = id,
                Name = name,
                ClientName = clientName,
                JackPortNames = jackPortNames.ToList()
            });

        await StoreConfigAsync();
        await TryLinkPortAsync(port);
    }

    public async Task DeleteJackInputPort(JackInputPort port)
    {
        lock (_lock)
        {
            _activePorts.Remove(port.Id);
            _configuredPorts.RemoveAll(c => c.Id == port.Id);
        }

        JackInputPorts.Remove(port);
        port.Loopback.Destroy();
        Deleted?.Invoke(port);
        await StoreConfigAsync();
    }

    private async Task<JackInputPort> CreatePortAsync(Guid id, string name,
        string clientName, string[] jackPortNames)
    {
        var fullName = $"{NodeNamePrefix}{name}";

        var loopback = await wireplumberService.CreateLoopbackModule(
            fullName, new LoopbackModuleConfig
            {
                CaptureProps = new NodePropertiesConfig
                {
                    Linger = true,
                    Name = $"{fullName}{CaptureNodeNameSuffix}",
                    Description = $"{fullName}{CaptureNodeNameSuffix}",
                    MediaClass = CaptureNodeMediaClass,
                    AudioPosition = StereoAudioPosition,
                    DontFallback = true,
                    AutoConnect = false,
                },
                PlaybackProps = new NodePropertiesConfig
                {
                    Linger = true,
                    Name = $"{fullName}{PlaybackNodeNameSuffix}",
                    Description = $"{fullName}{PlaybackNodeNameSuffix}",
                    MediaClass = PlaybackNodeMediaClass,
                    AudioPosition = StereoAudioPosition,
                    DontFallback = true,
                    AutoConnect = false,
                }
            });

        var port = new JackInputPort(id, name, clientName, jackPortNames, loopback);
        JackInputPorts.Add(port);
        lock (_lock)
            _activePorts[id] = port;

        Added?.Invoke(port);
        return port;
    }

    private async Task ApplyConfiguredPortsAsync()
    {
        await _restoreLock.WaitAsync();
        try
        {
            JackInputPortConfig[] pending;
            lock (_lock)
            {
                pending = _configuredPorts
                    .Where(config =>
                        !_activePorts.ContainsKey(config.Id) &&
                        _portsBeingCreated.Add(config.Id))
                    .ToArray();
            }

            foreach (var config in pending)
            {
                try
                {
                    var port = await CreatePortAsync(config.Id, config.Name,
                        config.ClientName, config.JackPortNames.ToArray());
                    await TryLinkPortAsync(port);
                }
                catch
                {
                    // Retry when the graph changes.
                }
                finally
                {
                    lock (_lock)
                        _portsBeingCreated.Remove(config.Id);
                }
            }
        }
        finally
        {
            _restoreLock.Release();
        }
    }

    private Task TryLinkPortAsync(JackInputPort port)
    {
        var jackNode = FrSonic.NodeRegistry.Objects.FirstOrDefault(n =>
            n.Client?.Api == "jack" &&
            string.Equals(n.Name, port.ClientName, StringComparison.Ordinal));

        if (jackNode is null)
            return Task.CompletedTask;

        return LinkPortToJackNodeAsync(port, jackNode);
    }

    private async Task TryLinkAllPortsAsync()
    {
        JackInputPort[] ports;
        lock (_lock)
            ports = _activePorts.Values.ToArray();

        foreach (var port in ports)
            await TryLinkPortAsync(port);
    }

    private async Task LinkPortToJackNodeAsync(JackInputPort port, Node jackNode)
    {
        var jackOutputPorts = FrSonic.PortRegistry.Objects
            .Where(p => p.Node.Id == jackNode.ObjectId && p.Direction == "out")
            .ToList();

        var captureNode = FrSonic.NodeRegistry.Objects.FirstOrDefault(n =>
            n.Name == $"{NodeNamePrefix}{port.Name}{CaptureNodeNameSuffix}");
        if (captureNode is null)
            return;

        // Wait briefly for capture ports to appear if the loopback was just created.
        List<Port> capturePorts = [];
        for (var attempt = 0; attempt < 20; attempt++)
        {
            capturePorts = FrSonic.PortRegistry.Objects
                .Where(p => p.Node.Id == captureNode.ObjectId && p.Direction == "in")
                .OrderBy(p => p.Name)
                .ToList();
            if (capturePorts.Count > 0)
                break;
            await Task.Delay(20);
        }

        for (var i = 0; i < port.JackPortNames.Count && i < capturePorts.Count; i++)
        {
            var jackPort = jackOutputPorts.FirstOrDefault(p =>
                string.Equals(p.Name, port.JackPortNames[i], StringComparison.Ordinal));
            if (jackPort is null)
                continue;

            var capturePort = capturePorts[i];

            var alreadyLinked = FrSonic.LinkRegistry.Objects.Any(link =>
                link.OutputPortId == jackPort.ObjectId &&
                link.InputPortId == capturePort.ObjectId);
            if (alreadyLinked)
                continue;

            FrSonicWireplumber.CreateLinkByPortIds(jackPort.ObjectId,
                capturePort.ObjectId, false);
        }
    }

    private async Task StoreConfigAsync()
    {
        await _storeLock.WaitAsync();
        try
        {
            JackInputPortConfig[] ports;
            lock (_lock)
                ports = _configuredPorts.ToArray();

            await appDataService.StoreJackInputPortsConfig(new JackInputPortsConfig
            {
                Ports = ports.ToList()
            });
        }
        finally
        {
            _storeLock.Release();
        }
    }

    private void OnNodeAdded(Node node)
    {
        if (node.Client?.Api != "jack")
            return;
        _ = TryLinkAllPortsAsync();
    }

    private void OnPortAdded(Port port)
    {
        var node = FrSonic.NodeRegistry.GetByObjectId(port.Node.Id);
        if (node is null || !IsJackInputPortCaptureNode(node))
            return;
        _ = TryLinkAllPortsAsync();
    }

    public void Dispose()
    {
        wireplumberService.NodeAdded -= OnNodeAdded;
        FrSonic.PortRegistry.Added -= OnPortAdded;
        _initializeLock.Dispose();
        _restoreLock.Dispose();
        _storeLock.Dispose();
    }
}
