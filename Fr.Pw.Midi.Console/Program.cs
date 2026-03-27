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

FrPwMidi.LayerChanged += (eventArgs) =>
{
    Console.WriteLine($"Set layer to {eventArgs.LayerId}");
};

FrPwMidi.DialSelectionModeChanged += (eventArgs) =>
{
    Console.WriteLine(
        $"Channel type {eventArgs.ChannelType}, channel id {eventArgs.ChannelId} set to {eventArgs.DialMode}");
};

FrPwMidi.FilterParamsSectionMovedLeft += (eventArgs) =>
{
    Console.WriteLine(
        $"Filter params moved left for {eventArgs.StepCount} step");
};

FrPwMidi.FilterParamsSectionMovedRight += (eventArgs) =>
{
    Console.WriteLine(
        $"Filter params moved right for {eventArgs.StepCount} step");
};

FrPwMidi.SelectedChannelChanged += (eventArgs) =>
{
    Console.WriteLine(
        $"Selected channel type {eventArgs.ChannelType}, channel id {eventArgs.ChannelId}");
};

FrPwMidi.SelectedFilterParamsSectionChanged += (eventArgs) =>
{
    Console.WriteLine(
        $"Channel type {eventArgs.ChannelType}, channel id {eventArgs.ChannelId}, selected Filter Params page section {eventArgs.SectionId}");
};

var midiMixPort = await midiPortFactory.CreateMidiMixPort();
var cmdMm1Port = await midiPortFactory.CreateCmdMm1Port();
var faderFoxPort = await midiPortFactory.CreateFaderFoxPc4Port();

await Task.Delay(TimeSpan.FromMinutes(30));

FrPwMidi.Stop();
Fr.Wireplumber.Wireplumber.Stop();