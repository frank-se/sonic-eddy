using System;
using System.IO;
using System.Reflection;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Fr.Sonic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ReactiveUI;
using Serilog;
using SonicEddy.Services.AppData;
using SonicEddy.Services.CameraRouter;
using SonicEddy.Services.StreamingControl;
using SonicEddy.Services.Gamepad;
using SonicEddy.Services.Midi;
using SonicEddy.Services.MidiRouter;
using SonicEddy.Services.MidiSync;
using SonicEddy.Services.ClickSync;
using SonicEddy.Services.DrumMixer;
using SonicEddy.Services.ExternalEffects;
using SonicEddy.Services.MixerServiceV2;
using SonicEddy.Services.MixerPersistence;
using SonicEddy.Services.MixerViewModels;
using SonicEddy.Services.Monitoring;
using SonicEddy.Services.Preferences;
using SonicEddy.Services.RecordingPickUp;
using SonicEddy.Services.SoomfonDeck;
using SonicEddy.Services.VirtualInputs;
using SonicEddy.Services.JackInputPorts;
using SonicEddy.Services.VirtualOutputs;
using SonicEddy.Services.TraktorZ1;
using SonicEddy.Services.VideoBlender;
using SonicEddy.Services.VideoStreaming;
using SonicEddy.Services.Wireplumber;
using SonicEddy.Tools;
using SonicEddy.ViewModels;
using SonicEddy.Views;
using Splat;

namespace SonicEddy;

public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        RegisterDependencies();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime
            desktop)
        {
            var mainWindowViewModel =
                Locator.Current.GetService<MainWindowViewModel>();
            var startupArgs = desktop.Args ?? Array.Empty<string>();
            var startupWindows = StartupWindowOptions.Parse(startupArgs);
            var mixerName = StartupWindowOptions.ParseMixerName(startupArgs);

            if (startupWindows.MainMixer)
            {
                desktop.MainWindow = new MainWindow
                {
                    DataContext = mainWindowViewModel
                };
            }

            desktop.Exit += (_, _) =>
            {
                (mainWindowViewModel as IDisposable)?.Dispose();
            };

            if (startupWindows.DrumMixer)
                mainWindowViewModel?.ShowDrumMixerWindow();
            if (startupWindows.Overview)
                mainWindowViewModel?.ShowMixerOverviewWindow();
            if (startupWindows.GlobalMaster)
                mainWindowViewModel?.ShowGlobalMasterWindow();
            if (startupWindows.MicChannel)
                mainWindowViewModel?.ShowMicChannelWindow();
            if (startupWindows.GlobalReturnChannels)
                mainWindowViewModel?.ShowGlobalReturnChannelsWindow();
            if (!string.IsNullOrWhiteSpace(mixerName))
                _ = mainWindowViewModel?.LoadOrCreateMixerByNameAsync(mixerName);

            if (startupWindows.StreamOverview && mainWindowViewModel is not null)
            {
                var loggerFactory = Locator.Current.GetService<ILoggerFactory>();
                var mixerOverviewStreamService = new MixerOverviewStreamService(
                    mainWindowViewModel,
                    loggerFactory?.CreateLogger<MixerOverviewStreamService>() ??
                    NullLogger<MixerOverviewStreamService>.Instance);
                Locator.CurrentMutable.Register<IMixerOverviewStreamService>(
                    () => mixerOverviewStreamService);
                mixerOverviewStreamService.Start();
                desktop.Exit += (_, _) => mixerOverviewStreamService.Dispose();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void RegisterDependencies()
    {
        var filterGraphPath = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "SonicEddy/FilterGraph");

        Directory.CreateDirectory(filterGraphPath);

        var mixerPath = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "SonicEddy/Mixer");

        Directory.CreateDirectory(mixerPath);

        var preferencesPath = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "SonicEddy/Preferences");

        Directory.CreateDirectory(preferencesPath);

        var presetPath = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "SonicEddy/FilterChainPreset");

        Directory.CreateDirectory(presetPath);

        var loggerFactory = CreateLoggerFactory();
        var logger = loggerFactory.CreateLogger<App>();

        logger.LogInformation("Logger initialized, starting App");

        Locator.CurrentMutable.Register(() => loggerFactory);

        var appDataService =
            new AppDataService(filterGraphPath, mixerPath, preferencesPath, presetPath);

        Locator.CurrentMutable.Register<IAppDataService>(() => appDataService);

        var preferencesService = new PreferenceService(appDataService);
        Locator.CurrentMutable.Register<IPreferenceService>(() =>
            preferencesService);

        var wireplumberService = new WireplumberService();
        Locator.CurrentMutable.Register<IWireplumberService>(() =>
            wireplumberService);

        var externalEffectService = new ExternalEffectService(appDataService,
            wireplumberService);
        Locator.CurrentMutable.Register<IExternalEffectService>(() =>
            externalEffectService);
        _ = externalEffectService.InitializeAsync();

        var virtualInputService =
            new VirtualInputService(appDataService, wireplumberService);
        Locator.CurrentMutable.Register<IVirtualInputService>(() =>
            virtualInputService);
        _ = virtualInputService.InitializeAsync();

        var virtualOutputService =
            new VirtualOutputService(appDataService, wireplumberService);
        Locator.CurrentMutable.Register<IVirtualOutputService>(() =>
            virtualOutputService);
        _ = virtualOutputService.InitializeAsync();

        var jackInputPortService =
            new JackInputPortService(appDataService, wireplumberService);
        Locator.CurrentMutable.Register<IJackInputPortService>(() =>
            jackInputPortService);
        _ = jackInputPortService.InitializeAsync();

        var monitoringService = new MonitoringService(FrSonic.Monitor,
            loggerFactory.CreateLogger<MonitoringService>());
        Locator.CurrentMutable.Register<IMonitoringService>(() =>
            monitoringService);

        var mixerServiceLogger = loggerFactory.CreateLogger<MixerService>();
        var mixerServiceV2 =
            new MixerService(appDataService,
                wireplumberService, preferencesService, externalEffectService,
                mixerServiceLogger);

        Locator.CurrentMutable
            .Register<IMixerService>(() =>
                mixerServiceV2);

        var mixerConfigurationService =
            new MixerConfigurationService(wireplumberService);
        Locator.CurrentMutable.Register(() => mixerConfigurationService);

        var monitoringLinkService = new MonitoringLinkService(mixerServiceV2);
        Locator.CurrentMutable.Register<IMonitoringLinkService>(() =>
            monitoringLinkService);

        var recordingPickUpService =
            new RecordingPickUpService(appDataService, wireplumberService, monitoringLinkService);
        Locator.CurrentMutable.Register<IRecordingPickUpService>(() =>
            recordingPickUpService);
        _ = recordingPickUpService.InitializeAsync();

        var midiControllerServiceLogger =
            loggerFactory.CreateLogger<MidiControllerService>();

        var midiControllerService =
            new MidiControllerService(midiControllerServiceLogger);

        var setupService = new MidiControllerSetupService();
        var midiRouterService = new MidiRouterService(appDataService);
        Locator.CurrentMutable.Register<IMidiRouterService>(() =>
            midiRouterService);
        _ = midiRouterService.InitializeAsync();

        var cameraRouterService = new CameraRouterService(appDataService);
        Locator.CurrentMutable.Register<ICameraRouterService>(() =>
            cameraRouterService);
        _ = cameraRouterService.InitializeAsync();

        // Two independent instances, one per T-bar M/E switcher panel (see
        // CompositorInstanceNames) - registered under Splat contracts A/B
        // rather than as a single unkeyed IStreamingControlService, since
        // there are genuinely two of them and their per-object state must
        // never merge (see MixEffectsSwitcherViewModel).
        var streamingControlServiceA = new StreamingControlService(
            CompositorInstanceNames.OutputNode(CompositorInstanceNames.A));
        Locator.CurrentMutable.Register<IStreamingControlService>(() =>
            streamingControlServiceA, CompositorInstanceNames.A);
        _ = streamingControlServiceA.InitializeAsync();

        var streamingControlServiceB = new StreamingControlService(
            CompositorInstanceNames.OutputNode(CompositorInstanceNames.B));
        Locator.CurrentMutable.Register<IStreamingControlService>(() =>
            streamingControlServiceB, CompositorInstanceNames.B);
        _ = streamingControlServiceB.InitializeAsync();

        // Third instance for the downstream effects (DSK) node - a
        // separate downstream-compositor process, not a pw-video-compositor
        // instance, so it uses its own fixed node name rather than
        // CompositorInstanceNames.OutputNode. Not part of
        // CompositorInstanceNames.All, since it's never routed through
        // CameraRouterService like A/B.
        var streamingControlServiceDownstream = new StreamingControlService(
            CompositorInstanceNames.DownstreamOutputNode);
        Locator.CurrentMutable.Register<IStreamingControlService>(() =>
            streamingControlServiceDownstream, CompositorInstanceNames.Downstream);
        _ = streamingControlServiceDownstream.InitializeAsync();

        // Gamepad is now permanently dedicated to the downstream node's
        // objects (see GamepadService's own class comment for why) rather
        // than switching between the two T-bar panels.
        var gamepadService = new GamepadService(appDataService, streamingControlServiceDownstream);
        Locator.CurrentMutable.Register<IGamepadService>(() =>
            gamepadService);
        _ = gamepadService.InitializeAsync();

        var videoBlenderService = new VideoBlenderService();
        Locator.CurrentMutable.Register<IVideoBlenderService>(() =>
            videoBlenderService);
        _ = videoBlenderService.InitializeAsync();

        // Always-on, like GamepadService/VideoBlenderService above - the
        // physical connection and read loop start at app launch regardless
        // of whether the Streaming Controls window is open; only the row
        // painting/dispatch logic (MixEffectsSwitcherViewModel) is
        // window-scoped.
        var soomfonDeckService = new SoomfonDeckService(
            loggerFactory.CreateLogger<SoomfonDeckService>());
        Locator.CurrentMutable.Register<ISoomfonDeckService>(() =>
            soomfonDeckService);
        var soomfonDeckConnectionManager = new SoomfonDeckConnectionManager(
            soomfonDeckService, loggerFactory.CreateLogger<SoomfonDeckConnectionManager>());
        Locator.CurrentMutable.Register(() => soomfonDeckConnectionManager);

        var midiSyncLinkService = new MidiSyncLinkService(appDataService);
        Locator.CurrentMutable.Register<IMidiSyncLinkService>(() =>
            midiSyncLinkService);
        _ = midiSyncLinkService.InitializeAsync();

        var clickSyncService = new ClickSyncService(appDataService);
        Locator.CurrentMutable.Register<IClickSyncService>(() =>
            clickSyncService);
        _ = clickSyncService.InitializeAsync();

        var drumMixerService = new DrumMixerService(appDataService,
            preferencesService,
            loggerFactory.CreateLogger<DrumMixerService>());
        Locator.CurrentMutable.Register<IDrumMixerService>(() =>
            drumMixerService);
        _ = drumMixerService.InitializeAsync();

        var mixerViewModelServiceLogger =
            loggerFactory.CreateLogger<MixerViewModelService>();
        var traktorZ1SetupService = new TraktorZ1SetupService();
        var traktorZ1Service = new TraktorZ1Service(traktorZ1SetupService,
            loggerFactory.CreateLogger<TraktorZ1Service>());

        var traktorZ1ConnectionManager = new TraktorZ1ConnectionManager(
            traktorZ1Service, preferencesService,
            loggerFactory.CreateLogger<TraktorZ1ConnectionManager>());
        Locator.CurrentMutable.Register<TraktorZ1ConnectionManager>(() =>
            traktorZ1ConnectionManager);

        var mixerViewModelService =
            new MixerViewModelService(appDataService, mixerServiceV2,
                monitoringService, mixerViewModelServiceLogger, loggerFactory,
                setupService, midiControllerService, traktorZ1SetupService);
        Locator.CurrentMutable.Register<IMixerViewModelService>(() =>
            mixerViewModelService);
        Locator.CurrentMutable.Register<ITraktorZ1SetupService>(() =>
            traktorZ1SetupService);

        Locator.CurrentMutable.RegisterViewsForViewModels(
            Assembly.GetExecutingAssembly());

        Locator.CurrentMutable.Register(() =>
            new MainWindowViewModel(midiControllerService,
                traktorZ1Service,
                loggerFactory.CreateLogger<MainWindowViewModel>(),
                loggerFactory));
    }

    private static ILoggerFactory CreateLoggerFactory()
    {
        Log.Logger = new LoggerConfiguration()
#if DEBUG
            .MinimumLevel.Verbose()
#else
            .MinimumLevel.Warning()
#endif
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .CreateLogger();

        return LoggerFactory.Create(builder =>
        {
            builder.AddSerilog(dispose: true);
        });
    }
}
