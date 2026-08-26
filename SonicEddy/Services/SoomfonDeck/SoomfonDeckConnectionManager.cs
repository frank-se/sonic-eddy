using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace SonicEddy.Services.SoomfonDeck;

/// <summary>
/// Keeps <see cref="ISoomfonDeckService"/> connected across reconnects -
/// same watch-/dev-for-hidraw-hotplug idiom as
/// <see cref="TraktorZ1.TraktorZ1ConnectionManager"/>, simplified (no
/// manual-path preference override - not needed for this device) and
/// adapted for multiple simultaneous hidraw paths.
/// </summary>
public sealed class SoomfonDeckConnectionManager : IDisposable
{
    private readonly ISoomfonDeckService _service;
    private readonly ILogger<SoomfonDeckConnectionManager> _logger;
    private readonly object _gate = new();

    private FileSystemWatcher? _watcher;
    private Timer? _debounce;
    private HashSet<string> _connectedPaths = [];

    public SoomfonDeckConnectionManager(ISoomfonDeckService service,
        ILogger<SoomfonDeckConnectionManager> logger)
    {
        _service = service;
        _logger = logger;

        Reconnect(SoomfonDeckLocator.FindHidrawPaths());
        StartWatching();
    }

    // Must be called with _gate held.
    private void Reconnect(IReadOnlyList<string> paths)
    {
        lock (_gate)
        {
            var pathSet = paths.ToHashSet();
            if (pathSet.SetEquals(_connectedPaths)) return;

            if (_connectedPaths.Count > 0)
            {
                _service.Stop();
                _logger.LogInformation("Soomfon deck disconnected from {Paths}",
                    string.Join(", ", _connectedPaths));
            }

            _connectedPaths = pathSet;

            if (paths.Count > 0)
                _service.Start(paths); // logs its own success/failure
        }
    }

    private void StartWatching()
    {
        if (_watcher is not null) return;
        if (!Directory.Exists("/dev")) return;

        try
        {
            var watcher = new FileSystemWatcher("/dev", "hidraw*")
            {
                NotifyFilter = NotifyFilters.FileName
            };
            watcher.Created += OnHidrawChanged;
            watcher.Deleted += OnHidrawChanged;
            watcher.Error += (_, e) =>
                _logger.LogWarning(e.GetException(), "Soomfon deck hidraw watcher error");
            watcher.EnableRaisingEvents = true;
            _watcher = watcher;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not watch /dev for Soomfon deck hotplug events");
        }
    }

    private void OnHidrawChanged(object? sender, FileSystemEventArgs e)
    {
        // Debounce: the deck exposes two HID interfaces, so plugging or
        // unplugging it fires a burst of create/delete events.
        _debounce?.Dispose();
        _debounce = new Timer(_ => Reconnect(SoomfonDeckLocator.FindHidrawPaths()),
            null, TimeSpan.FromMilliseconds(500), Timeout.InfiniteTimeSpan);
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _debounce?.Dispose();
        lock (_gate)
        {
            _service.Stop();
            _connectedPaths = [];
        }
    }
}
