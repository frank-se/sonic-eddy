using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Fr.Sonic;
using Fr.Sonic.Compositor;
using Fr.Sonic.Model.Objects;

namespace SonicEddy.Services.StreamingControl;

public sealed class StreamingControlService : IStreamingControlService, IDisposable
{
    private const string CompositorOutputNodeName = "se.video-compositor.out";

    private readonly object _lock = new();
    private readonly ConcurrentDictionary<string, Task<SceneFileConfig?>> _sceneFileCache = new();
    private CompositorClient? _client;
    private bool _initialized;

    public StreamingControlService()
    {
        FrSonic.NodeRegistry.Added += OnNodeAdded;
        FrSonic.NodeRegistry.Deleted += OnNodeDeleted;
    }

    public CompositorClient? Client
    {
        get
        {
            lock (_lock)
                return _client;
        }
    }

    public event Action? ConnectionChanged;

    public Task InitializeAsync()
    {
        lock (_lock)
        {
            if (_initialized) return Task.CompletedTask;
            _initialized = true;
        }

        var existing = FrSonic.NodeRegistry.Objects
            .FirstOrDefault(node => node.Name == CompositorOutputNodeName);
        if (existing is not null)
            Connect(existing);

        return Task.CompletedTask;
    }

    public Task<SceneFileConfig?> LoadSceneFileAsync(string path) =>
        _sceneFileCache.GetOrAdd(path, SceneFile.LoadAsync);

    private void OnNodeAdded(Node node)
    {
        if (node.Name != CompositorOutputNodeName) return;
        Connect(node);
    }

    private void OnNodeDeleted(Node node)
    {
        if (node.Name != CompositorOutputNodeName) return;

        bool changed;
        lock (_lock)
        {
            changed = _client is not null;
            _client?.Dispose();
            _client = null;
        }

        if (changed)
            ConnectionChanged?.Invoke();
    }

    private void Connect(Node node)
    {
        lock (_lock)
        {
            _client?.Dispose();
            _client = new CompositorClient(node);
        }

        ConnectionChanged?.Invoke();
    }

    public void Dispose()
    {
        FrSonic.NodeRegistry.Added -= OnNodeAdded;
        FrSonic.NodeRegistry.Deleted -= OnNodeDeleted;

        lock (_lock)
        {
            _client?.Dispose();
            _client = null;
        }
    }
}
