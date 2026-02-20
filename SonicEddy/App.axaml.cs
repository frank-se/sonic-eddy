using System;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using SonicEddy.Services.AppData;
using SonicEddy.Services.MixerData;
using SonicEddy.Services.MixerViewModels;
using SonicEddy.Services.Wireplumber;
using SonicEddy.ViewModels;
using SonicEddy.ViewModels.MixerViewModels;
using SonicEddy.ViewModels.ObjectBrowserViewModels;
using SonicEddy.ViewModels.ProAudioStreamsViewModels;
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

        var appDataService = new AppDataService(filterGraphPath, mixerPath);

        Locator.CurrentMutable.Register<IAppDataService>(() => appDataService);

        var mixerService = new MixerService(appDataService);
        Locator.CurrentMutable.Register<IMixerService>(() => mixerService);

        var wireplumberService = new WireplumberService();
        Locator.CurrentMutable.Register<IWireplumberService>(() =>
            wireplumberService);

        var mixerServiceV2 =
            new Services.MixerServiceV2.MixerService(appDataService,
                wireplumberService);
        Locator.CurrentMutable
            .Register<Services.MixerServiceV2.IMixerService>(() =>
                mixerServiceV2);

        var mixerViewModelService = new MixerViewModelService();
        Locator.CurrentMutable.Register<IMixerViewModelService>(() =>
            mixerViewModelService);

        Locator.CurrentMutable.Register(() => new MainWindowViewModel());
        Locator.CurrentMutable.Register<IViewLocator>(() => new ViewLocator());
    }
}