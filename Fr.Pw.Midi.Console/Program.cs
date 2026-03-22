using Fr.Pw.Midi;
using Fr.Pw.Midi.PInvoke;

Console.WriteLine("Fr Midi Console App");

Fr.Wireplumber.Wireplumber.Start();

var nodeRegistry = Fr.Wireplumber.Wireplumber.NodeRegistry;
var portRegistry = Fr.Wireplumber.Wireplumber.PortRegistry;
var linkFactory = Fr.Wireplumber.Wireplumber.LinkFactory;

FrPwMidi.Start(nodeRegistry, linkFactory, portRegistry);

var midiPortFactory = FrPwMidi.MidiPortFactory;

if (midiPortFactory is null)
    throw new ApplicationException("Midi port factory is null");

await Task.Delay(TimeSpan.FromSeconds(1));

var midiMixPort = await midiPortFactory.CreateMidiMixPort();

midiMixPort.LayerSelected +=
    layerId => Console.WriteLine($"Layer selected {layerId}");

midiMixPort.ChannelSelected += channelId =>
    Console.WriteLine($"Channel selected {channelId}");

midiMixPort.DialSelectionModeChanged += (ulong channelId, DialMode dialMode) =>
    Console.WriteLine($"Set dial mode {dialMode} for channel {channelId}");

midiMixPort.FilterParamsSectionChanged += (ulong channelId, ulong sectionId) =>
    Console.WriteLine(
        $"Filter params section {sectionId} activated for channel {channelId}");

await Task.Delay(TimeSpan.FromMinutes(30));

FrPwMidi.Stop();
Fr.Wireplumber.Wireplumber.Stop();