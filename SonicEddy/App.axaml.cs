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
using SonicEddy.Services.MixerServiceV2;
using SonicEddy.Services.MixerViewModels;
using SonicEddy.Services.Monitoring;
using SonicEddy.Services.Preferences;
using SonicEddy.Services.VirtualInputs;
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

        var monitoringLinkService = new MonitoringLinkService(mixerServiceV2);
        Locator.CurrentMutable.Register<IMonitoringLinkService>(() =>
            monitoringLinkService);

        var midiControllerServiceLogger =
            loggerFactory.CreateLogger<MidiControllerService>();

        var midiControllerService =
            new MidiControllerService(midiControllerServiceLogger);

        var setupService = new MidiControllerSetupService();
        var midiRouterService = new MidiRouterService();
        Locator.CurrentMutable.Register<IMidiRouterService>(() =>
            midiRouterService);

        var mixerViewModelServiceLogger =
            loggerFactory.CreateLogger<MixerViewModelService>();
        var traktorZ1SetupService = new TraktorZ1SetupService();
        var traktorZ1Service = new TraktorZ1Service(traktorZ1SetupService,
            loggerFactory.CreateLogger<TraktorZ1Service>());
        traktorZ1Service.Start("/dev/hidraw3");

        var mixerViewModelService =
            new MixerViewModelService(appDataService, mixerServiceV2,
                monitoringService, mixerViewModelServiceLogger, loggerFactory,
                setupService, midiControllerService, traktorZ1SetupService);
        Locator.CurrentMutable.Register<IMixerViewModelService>(() =>
            mixerViewModelService);

        Locator.CurrentMutable.RegisterViewsForViewModels(
            Assembly.GetExecutingAssembly());

        Locator.CurrentMutable.Register(() =>
            new MainWindowViewModel(midiControllerService,
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
