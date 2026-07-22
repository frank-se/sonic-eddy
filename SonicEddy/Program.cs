using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Fr.Sonic;
using Fr.Sonic.Sync;
using ReactiveUI.Avalonia;
using SonicEddy.Services.DrumMixer;
using SonicEddy.Services.ExternalEffects;
using SonicEddy.Services.MixerServiceV2;
using Splat;

namespace SonicEddy;

internal static class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things
    // aren't initialized yet, and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        FrSonic.Init(TimeSpan.FromMilliseconds(250));
        FrSonic.Start();
        FrSonicLv2.Init();
        AbletonLink.Initialize();

        /*
         * TODO: Update to wait for the availability of the midi bridge directly
         * instead
         */
        Thread.Sleep(TimeSpan.FromSeconds(1));
        var midiPortFactory = FrSonic.MidiPortFactory;

        try
        {
            var midiMixPort = midiPortFactory!.CreateMidiMixPort().Result;
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }

        try
        {
            var cmdMm1Port = midiPortFactory!.CreateCmdMm1Port().Result;
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }

        try
        {
            var faderFoxPort = midiPortFactory!.CreateFaderFoxPc4Port().Result;
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }

        try
        {
            var launchpadMiniPort = midiPortFactory!.CreateLaunchpadMiniPort().Result;
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);

        (Locator.Current.GetService<IMixerService>() as IDisposable)?.Dispose();
        (Locator.Current.GetService<IExternalEffectService>() as IDisposable)
            ?.Dispose();
        (Locator.Current.GetService<IDrumMixerService>() as IDisposable)
            ?.Dispose();
        FrSonicLv2.Destroy();
        FrSonic.Stop();
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    private static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UseWayland()
            .UseSkia()
            .UseHarfBuzz()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI(_ => { });
    }
}
