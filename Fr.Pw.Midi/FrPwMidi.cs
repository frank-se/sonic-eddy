using Fr.Pw.Midi.Mappings;
using Fr.Pw.Midi.MidiPorts;
using Fr.Pw.Midi.MidiPorts.Implementation;
using Fr.Pw.Midi.PInvoke;
using Fr.Wireplumber.Factories;
using Fr.Wireplumber.Registries.Nodes;
using Fr.Wireplumber.Registries.Ports;

namespace Fr.Pw.Midi;

public record LayerSelectEventArgs(ulong LayerId);

public record ChannelSelectEventArgs(ChannelType ChannelType, ulong ChannelId);

public record DialSelectionModeSelectEventArgs(
    ChannelType ChannelType,
    ulong ChannelId,
    DialMode DialMode);

public record FilterParamsSectionSelectEventArgs(
    ChannelType ChannelType,
    ulong ChannelId,
    ulong SectionId);

public record FilterParamsSectionMovePagesEventArgs(ulong StepCount);

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
            linkFactory, portRegistry, OnLayerSelect, OnChannelSelect,
            OnDialSectionModeSelect, OnFilterParamsSectionSelect,
            OnFilterParamsSectionMoveRight, OnFilterParamsSectionMoveLeft);

        FrPwMidiLib.Init();
        FrPwMidiLib.Start();
    }

    public static event Action<LayerSelectEventArgs>? LayerChanged;
    public static event Action<ChannelSelectEventArgs>? SelectedChannelChanged;

    public static event Action<DialSelectionModeSelectEventArgs>?
        DialSelectionModeChanged;

    public static event Action<FilterParamsSectionSelectEventArgs>?
        SelectedFilterParamsSectionChanged;

    public static event Action<FilterParamsSectionMovePagesEventArgs>?
        FilterParamsSectionMovedRight;

    public static event Action<FilterParamsSectionMovePagesEventArgs>?
        FilterParamsSectionMovedLeft;

    public static void Stop() => FrPwMidiLib.Stop();

    private static void OnLayerSelect(ulong layerId)
    {
        LayerChanged?.Invoke(new(layerId));
    }

    private static void OnChannelSelect(ChannelType channelType,
        ulong channelId)
    {
        SelectedChannelChanged?.Invoke(new(channelType, channelId));
    }

    private static void OnDialSectionModeSelect(ChannelType channelType,
        ulong channelId, DialMode dialMode)
    {
        DialSelectionModeChanged?.Invoke(new(channelType, channelId, dialMode));
    }

    private static void OnFilterParamsSectionSelect(ChannelType channelType,
        ulong channelId, ulong sectionId)
    {
        SelectedFilterParamsSectionChanged?.Invoke(new(channelType, channelId,
            sectionId));
    }

    private static void OnFilterParamsSectionMoveRight(ulong stepCount)
    {
        FilterParamsSectionMovedRight?.Invoke(new(stepCount));
    }

    private static void OnFilterParamsSectionMoveLeft(ulong stepCount)
    {
        FilterParamsSectionMovedLeft?.Invoke(new(stepCount));
    }
}