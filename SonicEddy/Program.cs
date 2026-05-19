using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Fr.Sonic;
using ReactiveUI.Avalonia;

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

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);

        FrSonic.Stop();
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    private static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI(_ => { });
    }
}
