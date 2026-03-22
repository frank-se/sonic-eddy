using Fr.Pw.Midi.Mappings;
using Fr.Pw.Midi.MidiPorts;
using Fr.Pw.Midi.MidiPorts.Implementation;
using Fr.Pw.Midi.PInvoke;
using Fr.Wireplumber.Factories;
using Fr.Wireplumber.Registries.Nodes;
using Fr.Wireplumber.Registries.Ports;

namespace Fr.Pw.Midi;

public static class FrPwMidi
{
    public static IMidiPortRegistry? MidiPortRegistry { get; private set; }
    public static IMidiPortFactory? MidiPortFactory { get; private set; }

    public static void Start(NodeRegistry nodeRegistry,
        ILinkFactory linkFactory,
        PortRegistry portRegistry)
    {
        var midiPortRegistry = new MidiPortRegistry();

        MidiPortRegistry = midiPortRegistry;
        MidiPortFactory = new MidiPortFactory(nodeRegistry, midiPortRegistry,
            linkFactory, portRegistry);

        FrPwMidiLib.Init();
        FrPwMidiLib.Start();
    }

    public static void Stop() => FrPwMidiLib.Stop();
}