using System;
using System.Linq;
using System.Threading.Tasks;
using Fr.Sonic;
using Fr.Sonic.Compositor;
using Fr.Sonic.Model.Objects;

namespace SonicEddy.Services.VideoBlender;

public sealed class VideoBlenderService : IVideoBlenderService, IDisposable
{
    private const string OutputNodeName = "se.video-blender.out";

    private readonly object _lock = new();
    private VideoBlenderClient? _client;
    private bool _initialized;

    public VideoBlenderService()
    {
        FrSonic.NodeRegistry.Added += OnNodeAdded;
        FrSonic.NodeRegistry.Deleted += OnNodeDeleted;
    }

    public VideoBlenderClient? Client
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
            .FirstOrDefault(node => node.Name == OutputNodeName);
        if (existing is not null)
            Connect(existing);

        return Task.CompletedTask;
    }

    private void OnNodeAdded(Node node)
    {
        if (node.Name != OutputNodeName) return;
        Connect(node);
    }

    private void OnNodeDeleted(Node node)
    {
        if (node.Name != OutputNodeName) return;

        bool changed;
        lock (_lock)
        {
            changed = _client is not null;
            _client = null;
        }

        if (changed)
            ConnectionChanged?.Invoke();
    }

    private void Connect(Node node)
    {
        lock (_lock)
            _client = new VideoBlenderClient(node);

        ConnectionChanged?.Invoke();
    }

    public void Dispose()
    {
        FrSonic.NodeRegistry.Added -= OnNodeAdded;
        FrSonic.NodeRegistry.Deleted -= OnNodeDeleted;
    }
}
