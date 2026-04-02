using System;
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

        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(1));

            var midiPortFactory = FrPwMidi.MidiPortFactory;
            var midiMixPort = await midiPortFactory!.CreateMidiMixPort();
            var cmdMm1Port = await midiPortFactory.CreateCmdMm1Port();
            var faderFoxPort = await midiPortFactory.CreateFaderFoxPc4Port();
        });

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