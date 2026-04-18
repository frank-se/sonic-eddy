using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Fr.Lv2;
using Fr.Pw.Midi;
using Fr.Pw.Monitoring;
using Fr.Wireplumber;
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
        Wireplumber.Start();
        Lv2.Init();
        FrPwMonitoring.Start(TimeSpan.FromMilliseconds(250));

        var nodeRegistry = Wireplumber.NodeRegistry;
        var portRegistry = Wireplumber.PortRegistry;
        var linkFactory = Wireplumber.LinkFactory;

        FrPwMidi.Start(nodeRegistry, linkFactory, portRegistry);

        /*
         * TODO: Update to wait for the availability of the midi bridge directly
         * instead
         */
        Thread.Sleep(TimeSpan.FromSeconds(1));
        var midiPortFactory = FrPwMidi.MidiPortFactory;

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

        FrPwMidi.Stop();
        FrPwMonitoring.Stop();
        Lv2.Destroy();
        Wireplumber.Stop();
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