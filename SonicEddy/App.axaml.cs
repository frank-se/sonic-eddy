using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using SonicEddy.Services.AppData;
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
        Locator.CurrentMutable.Register<IAppDataService, AppDataService>();
        Locator.CurrentMutable.Register(() =>
        {
            var appService = Locator.Current.GetService<IAppDataService>();
            return new ObjectBrowserViewModel(appService!);
        });

        Locator.CurrentMutable.Register(() =>
        {
            var appService = Locator.Current.GetService<IAppDataService>();
            return new ProAudioStreamsViewModel(appService!);
        });

        Locator.CurrentMutable.Register(() =>
        {
            var appService = Locator.Current.GetService<IAppDataService>();
            return new MixerViewModel(appService!);
        });

        Locator.CurrentMutable.Register(() => new MainWindowViewModel());
    }
}