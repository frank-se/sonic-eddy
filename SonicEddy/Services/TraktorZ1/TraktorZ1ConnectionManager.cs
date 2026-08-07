using System;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Logging;
using SonicEddy.Services.Preferences;

namespace SonicEddy.Services.TraktorZ1;

/// <summary>
/// Decides which /dev/hidrawN node <see cref="ITraktorZ1Service"/> should
/// use and keeps it connected across reconnects.
///
/// If <c>TraktorZ1HidrawPath</c> is set in preferences it is used verbatim
/// (manual override, e.g. for unusual setups). Otherwise the Z1 is
/// auto-detected by USB vendor/product ID via
/// <see cref="TraktorZ1DeviceLocator"/>, and /dev is watched so unplug/
/// replug (which can change the hidraw node number) is picked up without
/// restarting the app.
/// </summary>
public sealed class TraktorZ1ConnectionManager : IDisposable
{
    private readonly ITraktorZ1Service _service;
    private readonly IPreferenceService _preferences;
    private readonly ILogger<TraktorZ1ConnectionManager> _logger;
    private readonly object _gate = new();

    private static readonly TimeSpan StartupGracePeriod =
        TimeSpan.FromSeconds(20);

    private FileSystemWatcher? _watcher;
    private Timer? _debounce;
    private Timer? _startupWarningTimer;
    private string _manualPath    = "";
    private string _connectedPath = "";

    public TraktorZ1ConnectionManager(ITraktorZ1Service service,
        IPreferenceService preferences,
        ILogger<TraktorZ1ConnectionManager> logger)
    {
        _service     = service;
        _preferences = preferences;
        _logger      = logger;

        _preferences.Changed += OnPreferencesChanged;
        OnPreferencesChanged();

        _startupWarningTimer = new Timer(CheckStartupWarning, null,
            StartupGracePeriod, Timeout.InfiniteTimeSpan);
    }

    private void CheckStartupWarning(object? state)
    {
        lock (_gate)
        {
            if (string.IsNullOrEmpty(_connectedPath))
                _logger.LogWarning(
                    "Traktor Z1 not detected {Seconds}s after startup — " +
                    "waiting for it to be plugged in",
                    (int)StartupGracePeriod.TotalSeconds);
        }

        _startupWarningTimer?.Dispose();
        _startupWarningTimer = null;
    }

    private void OnPreferencesChanged()
    {
        lock (_gate)
        {
            _manualPath = _preferences.Preferences?.TraktorZ1HidrawPath ?? "";

            if (!string.IsNullOrEmpty(_manualPath))
            {
                StopWatching();
                Reconnect(_manualPath);
            }
            else
            {
                Reconnect(TraktorZ1DeviceLocator.FindHidrawPath() ?? "");
                StartWatching();
            }
        }
    }

    // Must be called with _gate held.
    private void Reconnect(string path)
    {
        if (path == _connectedPath) return;

        if (!string.IsNullOrEmpty(_connectedPath))
        {
            _service.Stop();
            _logger.LogInformation("Traktor Z1 disconnected from {Path}",
                _connectedPath);
        }

        _connectedPath = path;

        if (!string.IsNullOrEmpty(path))
            _service.Start(path); // logs its own success/failure
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
            watcher.Created             += OnHidrawChanged;
            watcher.Deleted             += OnHidrawChanged;
            watcher.Error               += (_, e) =>
                _logger.LogWarning(e.GetException(),
                    "Traktor Z1 hidraw watcher error");
            watcher.EnableRaisingEvents =  true;
            _watcher                    =  watcher;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Could not watch /dev for Traktor Z1 hotplug events");
        }
    }

    private void StopWatching()
    {
        _watcher?.Dispose();
        _watcher = null;
        _debounce?.Dispose();
        _debounce = null;
    }

    private void OnHidrawChanged(object? sender, FileSystemEventArgs e)
    {
        // Debounce: the Z1 exposes several HID interfaces, so plugging or
        // unplugging it fires a burst of create/delete events.
        _debounce?.Dispose();
        _debounce = new Timer(_ =>
        {
            lock (_gate)
            {
                if (!string.IsNullOrEmpty(_manualPath)) return;
                Reconnect(TraktorZ1DeviceLocator.FindHidrawPath() ?? "");
            }
        }, null, TimeSpan.FromMilliseconds(500), Timeout.InfiniteTimeSpan);
    }

    public void Dispose()
    {
        _preferences.Changed -= OnPreferencesChanged;
        _startupWarningTimer?.Dispose();
        _startupWarningTimer = null;
        lock (_gate)
        {
            StopWatching();
            _service.Stop();
            _connectedPath = "";
        }
    }
}
