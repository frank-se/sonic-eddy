using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Fr.Sonic;
using Microsoft.Extensions.Logging;
using SonicEddy.ViewModels;
using SonicEddy.ViewModels.MixerOverviewViewModels;
using SonicEddy.Views.MixerOverviewViews;

namespace SonicEddy.Services.VideoStreaming;

// Renders the mixer overview UI off-screen at a fixed rate and pushes the
// result into PipeWire as a plain video source node (via FrSonicVideoProducer),
// independent of whether the user has the real MixerOverviewWindow open -
// this is the "screen share our own state" path, not OS screen capture.
//
// The rendered window must actually be Show()n (positioned off-screen, not
// in the taskbar) - an un-shown Window's Measure/Arrange succeeds but
// RenderTargetBitmap.Render() on it produces a fully blank/transparent
// bitmap, verified empirically.
public sealed class MixerOverviewStreamService(
    MainWindowViewModel mainWindowViewModel,
    ILogger<MixerOverviewStreamService> logger)
    : IMixerOverviewStreamService, IDisposable
{
    private const int FrameIntervalMs = 66; // ~15fps

    private MixerOverviewViewModel? _overviewViewModel;
    private Window? _window;
    private FrSonicVideoProducer? _producer;
    private DispatcherTimer? _timer;
    private byte[]? _pixelBuffer;
    private int _width;
    private int _height;

    public bool IsRunning => _timer is not null;

    public void Start()
    {
        if (IsRunning) return;
        _ = StartAsync();
    }

    private async System.Threading.Tasks.Task StartAsync()
    {
        var overviewViewModel =
            await mainWindowViewModel.CreateMixerOverviewViewModelAsync();
        if (overviewViewModel is null)
        {
            logger.LogWarning(
                "MixerOverviewStreamService: could not create overview view model");
            return;
        }

        Dispatcher.UIThread.Post(() => StartOnUiThread(overviewViewModel));
    }

    private void StartOnUiThread(MixerOverviewViewModel overviewViewModel)
    {
        if (IsRunning)
        {
            overviewViewModel.Dispose();
            return;
        }

        _overviewViewModel = overviewViewModel;
        _window = new MixerOverviewWindow
        {
            DataContext = overviewViewModel,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Position = new PixelPoint(-10000, -10000),
        };
        _window.Show();

        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(FrameIntervalMs),
            DispatcherPriority.Background, OnTick);
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_window is null) return;

        var width = Math.Max(0, (int)Math.Ceiling(_window.Bounds.Width));
        var height = Math.Max(0, (int)Math.Ceiling(_window.Bounds.Height));
        if (width == 0 || height == 0) return; // not laid out yet

        if (_producer is null || width != _width || height != _height)
        {
            _producer?.Dispose();
            _width = width;
            _height = height;
            _pixelBuffer = new byte[width * height * 4];
            _producer = FrSonicVideoProducer.Create("se.mixer-overview",
                "Sonic Eddy mixer overview", (uint)width, (uint)height);
            if (_producer is null)
            {
                logger.LogWarning(
                    "MixerOverviewStreamService: failed to create native video producer ({Width}x{Height})",
                    width, height);
                return;
            }
        }

        using var frame = new RenderTargetBitmap(new PixelSize(width, height));
        frame.Render(_window);

        var stride = width * 4;
        var pinned = GCHandle.Alloc(_pixelBuffer, GCHandleType.Pinned);
        try
        {
            frame.CopyPixels(new PixelRect(0, 0, width, height),
                pinned.AddrOfPinnedObject(), _pixelBuffer!.Length, stride);
        }
        finally
        {
            pinned.Free();
        }

        // RenderTargetBitmap is Bgra8888 on this rendering backend (verified
        // empirically) - our video producer expects Rgba8888, matching the
        // compositor's fixed format.
        if (frame.Format?.Equals(PixelFormat.Bgra8888) == true)
            SwapRedBlue(_pixelBuffer!);

        _producer.UpdateFrame(_pixelBuffer!);
    }

    private static void SwapRedBlue(byte[] pixels)
    {
        for (var i = 0; i + 3 < pixels.Length; i += 4)
            (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);
    }

    public void Stop()
    {
        if (!IsRunning) return;

        _timer?.Stop();
        _timer = null;
        _producer?.Dispose();
        _producer = null;
        _window?.Close();
        _window = null;
        _overviewViewModel?.Dispose();
        _overviewViewModel = null;
        _pixelBuffer = null;
        _width = 0;
        _height = 0;
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}
