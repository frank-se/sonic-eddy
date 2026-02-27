using System;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using SonicEddy.Services.AppData;
using SonicEddy.Services.MixerData;
using SonicEddy.Services.MixerViewModels;
using SonicEddy.Services.Preferences;
using SonicEddy.Services.VirtualInputs;
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

        var appDataService =
            new AppDataService(filterGraphPath, mixerPath, preferencesPath);

        Locator.CurrentMutable.Register<IAppDataService>(() => appDataService);

        var preferencesService = new PreferenceService(appDataService);
        Locator.CurrentMutable.Register<IPreferenceService>(() =>
            preferencesService);

        var mixerService = new MixerService(appDataService);
        Locator.CurrentMutable.Register<IMixerService>(() => mixerService);

        var wireplumberService = new WireplumberService();
        Locator.CurrentMutable.Register<IWireplumberService>(() =>
            wireplumberService);

        var virtualInputService =
            new VirtualInputService(appDataService, wireplumberService);
        Locator.CurrentMutable.Register<IVirtualInputService>(() =>
            virtualInputService);

        var mixerServiceV2 =
            new Services.MixerServiceV2.MixerService(appDataService,
                wireplumberService, preferencesService);

        Locator.CurrentMutable
            .Register<Services.MixerServiceV2.IMixerService>(() =>
                mixerServiceV2);

        var mixerViewModelService =
            new MixerViewModelService(appDataService, mixerServiceV2);
        Locator.CurrentMutable.Register<IMixerViewModelService>(() =>
            mixerViewModelService);

        Locator.CurrentMutable.Register(() => new MainWindowViewModel());
        Locator.CurrentMutable.Register<IViewLocator>(() => new ViewLocator());
    }
}