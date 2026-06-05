using System;
using System.IO;
using System.Reflection;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Fr.Sonic;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Serilog;
using SonicEddy.Services.AppData;
using SonicEddy.Services.Midi;
using SonicEddy.Services.MidiRouter;
using SonicEddy.Services.MidiSync;
using SonicEddy.Services.ClickSync;
using SonicEddy.Services.MixerServiceV2;
using SonicEddy.Services.MixerPersistence;
using SonicEddy.Services.MixerViewModels;
using SonicEddy.Services.Monitoring;
using SonicEddy.Services.Preferences;
using SonicEddy.Services.VirtualInputs;
using SonicEddy.Services.VirtualOutputs;
using SonicEddy.Services.TraktorZ1;
using SonicEddy.Services.Wireplumber;
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
            desktop.MainWindow = new MainWindow
            {
                DataContext = Locator.Current.GetService<MainWindowViewModel>()
            };

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

        var monitoringService = new MonitoringService(FrSonic.Monitor,
            loggerFactory.CreateLogger<MonitoringService>());
        Locator.CurrentMutable.Register<IMonitoringService>(() =>
            monitoringService);

        var mixerServiceLogger = loggerFactory.CreateLogger<MixerService>();
        var mixerServiceV2 =
            new MixerService(appDataService,
                wireplumberService, preferencesService, mixerServiceLogger);

        Locator.CurrentMutable
            .Register<IMixerService>(() =>
                mixerServiceV2);

        var mixerConfigurationService =
            new MixerConfigurationService(wireplumberService);
        Locator.CurrentMutable.Register(() => mixerConfigurationService);

        var monitoringLinkService = new MonitoringLinkService(mixerServiceV2);
        Locator.CurrentMutable.Register<IMonitoringLinkService>(() =>
            monitoringLinkService);

        var midiControllerServiceLogger =
            loggerFactory.CreateLogger<MidiControllerService>();

        var midiControllerService =
            new MidiControllerService(midiControllerServiceLogger);

        var setupService = new MidiControllerSetupService();
        var midiRouterService = new MidiRouterService(appDataService);
        Locator.CurrentMutable.Register<IMidiRouterService>(() =>
            midiRouterService);
        _ = midiRouterService.InitializeAsync();

        var midiSyncLinkService = new MidiSyncLinkService(appDataService);
        Locator.CurrentMutable.Register<IMidiSyncLinkService>(() =>
            midiSyncLinkService);
        _ = midiSyncLinkService.InitializeAsync();

        var clickSyncService = new ClickSyncService(appDataService);
        Locator.CurrentMutable.Register<IClickSyncService>(() =>
            clickSyncService);
        _ = clickSyncService.InitializeAsync();

        var mixerViewModelServiceLogger =
            loggerFactory.CreateLogger<MixerViewModelService>();
        var traktorZ1SetupService = new TraktorZ1SetupService();
        var traktorZ1Service = new TraktorZ1Service(traktorZ1SetupService,
            loggerFactory.CreateLogger<TraktorZ1Service>());

        var traktorZ1CurrentPath = "";
        preferencesService.Changed += () =>
        {
            var newPath = preferencesService.Preferences?.TraktorZ1HidrawPath ?? "";
            if (newPath == traktorZ1CurrentPath) return;
            traktorZ1Service.Stop();
            traktorZ1CurrentPath = newPath;
            if (!string.IsNullOrEmpty(newPath))
                traktorZ1Service.Start(newPath);
        };

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
            .MinimumLevel.Verbose()
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .CreateLogger();

        return LoggerFactory.Create(builder =>
        {
            builder.AddSerilog(dispose: true);
        });
    }
}
