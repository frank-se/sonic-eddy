using System;
using Avalonia;
using Fr.Lv2;
using Fr.Wireplumber;
using ReactiveUI.Avalonia;

namespace SonicEddy;

internal sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things
    // aren't initialized yet, and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        Wireplumber.Start();
        Lv2.Init();

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);

        Wireplumber.Stop();
        Lv2.Destroy();
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
    }
}